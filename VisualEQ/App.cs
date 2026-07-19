using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using ImageLib;
using MoreLinq;
using VisualEQ.Common;
using VisualEQ.Engine;
using VisualEQ.Materials;
using VisualEQ.Settings;
using VisualEQ.Views;
using static System.Console;

namespace VisualEQ
{
    internal class App
    {
        // %APPDATA%\VisualEQ\crash.log — appended on any unhandled exception. Keeps a
        // durable trace even when the console window is torn down with the process
        // (native crash, hard fault) so post-mortem debugging doesn't depend on catching
        // stdout live.
        static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualEQ", "crash.log");

        static void LogCrash(string origin, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath));
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {origin}\n{ex}\n\n";
                File.AppendAllText(CrashLogPath, msg);
                Console.Error.WriteLine(msg);
            }
            catch (Exception logEx)
            {
                Console.Error.WriteLine($"[LogCrash] failed to write to {CrashLogPath}: {logEx.Message}");
            }
        }

        static void Main(string[] args)
        {
            // Catch-alls for exceptions the try/catch below won't see — anything thrown
            // from OpenTK render/update callbacks or ThreadPool continuations bypasses the
            // main-thread frame. Log to file so the user can share the trace after a crash.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogCrash("UnhandledException", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown"));
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogCrash("UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            try
            {
                var settings = SettingsManager.Load();
                Console.WriteLine($"Settings loaded from {SettingsManager.SettingsPath}");
                Console.WriteLine($"Crash log: {CrashLogPath}");

                var controller = new Controller(settings);
                controller.Engine.Controller = controller;

                // --list-models is a diagnostic path that never opens the window.
                if (args.Length >= 2 && args[1].ToLower() == "--list-models")
                {
                    ListAvailableModels(args[0], controller.ConvertedAssetsDir);
                    return;
                }

                // With a zone name argument, preserve the legacy load-and-go flow so
                // load_zone.bat and any existing scripts keep working.
                if (args.Length >= 1)
                {
                    string zoneName = args[0];
                    string modelName = args.Length >= 2 ? args[1] : null;

                    Console.WriteLine($"Loading {zoneName}");
                    if (modelName != null)
                        Console.WriteLine($"Will try to load character model: {modelName}");

                    controller.LoadZone(zoneName);
                    LoadCharacters(controller, zoneName, modelName);
                    controller.LoadZoneSpawnsAsync(zoneName).GetAwaiter().GetResult();
                }
                else
                {
                    Console.WriteLine("No zone specified — opening main menu.");
                }

                // MainMenuView renders while no zone is loaded; SidebarView takes over when
                // a zone loads and consolidates the old Status/Teleport/ModelEditor windows
                // into a pinned left panel with collapsible sections.
                controller.AddView(new MainMenuView(controller));
                controller.AddView(new SidebarView(controller));
                // F1 cheat sheet. Registered here (not by Controller) so the
                // reference lives on Controller.HelpView for the F1 hotkey to
                // reach. Widget self-gates on Visible so registering it always
                // is safe — one branch per frame when hidden.
                var helpView = new HelpView(controller);
                controller.AddView(helpView);
                controller.HelpView = helpView;

                controller.Start();

                // Bypass finalization. EngineCore.OnUnload already released the heavy
                // GL caches while the context was current; anything left is either OS-
                // reclaimable (sockets, file handles) or a wrapper whose finalizer would
                // just poke a dead GL context. On Parallels/ARM64 that finalizer pass is
                // where the "close takes forever after long Kunark session" was coming
                // from — skipping it drops shutdown to effectively instant.
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LogCrash("Main try/catch", ex);
                Console.WriteLine($"Error starting application: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // New method to list all available models without loading them
        private static void ListAvailableModels(string zoneName, string assetsDir)
        {
            // Define possible character file prefixes to try
            string[] characterFilePrefixes = new string[] {
                $"{zoneName}_chr",         // Zone-specific characters
				"gfaydark_chr"             // Default characters
			};

            Console.WriteLine("====================================");
            Console.WriteLine($"AVAILABLE CHARACTER MODELS FOR ZONE: {zoneName}");
            Console.WriteLine("====================================");

            bool modelsFound = false;

            // Try each prefix until one works
            foreach (string prefix in characterFilePrefixes)
            {
                string characterPath = Path.Combine(assetsDir, $"{prefix}_oes.zip");

                if (File.Exists(characterPath))
                {
                    try
                    {
                        Console.WriteLine($"\nModels in {prefix}_oes.zip:");
                        Console.WriteLine("---------------------------");

                        // Try opening the zip file to make sure it's valid
                        using (var zipFile = ZipFile.OpenRead(characterPath))
                        {
                            // Open main.oes to extract available models
                            using (var stream = new MemoryStream())
                            {
                                using (var entryStream = zipFile.GetEntry("main.oes")?.Open())
                                {
                                    if (entryStream == null)
                                    {
                                        Console.WriteLine("  Error: main.oes not found in zip file.");
                                        continue;
                                    }
                                    entryStream.CopyTo(stream);
                                }

                                // Reset the memory stream position to the beginning
                                stream.Position = 0;

                                try
                                {
                                    // Read the OES file structure
                                    var root = OESFile.Read<OESRoot>(stream);

                                    // Extract character model names
                                    var characterModels = root.Find<OESCharacter>().ToList();

                                    if (characterModels.Count == 0)
                                    {
                                        Console.WriteLine("  No character models found.");
                                    }
                                    else
                                    {
                                        foreach (var model in characterModels)
                                        {
                                            Console.WriteLine($"  - {model.Name}");
                                        }
                                        modelsFound = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"  Error parsing main.oes: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error accessing {prefix}_oes.zip: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"\nFile not found: {characterPath}");
                }
            }

            if (!modelsFound)
            {
                Console.WriteLine("\nNo character models were found for this zone.");
                Console.WriteLine("You may need to convert character models first.");
            }

            Console.WriteLine("\nTo use a specific model when loading a zone:");
            Console.WriteLine("dotnet run <zone_name> <model_name>");
            Console.WriteLine("\nExample: dotnet run gfaydark ORC");
        }

        // Helper method to find and load character models
        private static void LoadCharacters(Controller controller, string zoneName, string requestedModel = null)
        {
            // Define possible character file prefixes to try
            string[] characterFilePrefixes = new string[] {
                $"{zoneName}_chr",         // Zone-specific characters
				"gfaydark_chr"             // Default characters
			};

            bool characterLoaded = false;

            // Keep track of all available models across all files
            List<(string File, string Model)> availableModels = new List<(string, string)>();

            // Try each prefix until one works
            foreach (string prefix in characterFilePrefixes)
            {
                string characterPath = Path.Combine(controller.ConvertedAssetsDir, $"{prefix}_oes.zip");

                if (File.Exists(characterPath))
                {
                    try
                    {
                        // Try opening the zip file to make sure it's valid
                        using (var zipFile = ZipFile.OpenRead(characterPath))
                        {
                            // Open main.oes to extract available models
                            using (var stream = new MemoryStream())
                            {
                                using (var entryStream = zipFile.GetEntry("main.oes")?.Open())
                                {
                                    if (entryStream == null) continue;
                                    entryStream.CopyTo(stream);
                                }

                                // Reset the memory stream position to the beginning
                                stream.Position = 0;

                                // Read the OES file structure
                                var root = OESFile.Read<OESRoot>(stream);

                                // Extract character model names
                                var characterModels = root.Find<OESCharacter>().ToList();

                                // If we found models, add them to our available models list
                                foreach (var model in characterModels)
                                {
                                    availableModels.Add((prefix, model.Name));
                                }

                                // Try to load the requested model if specified and available in this file
                                if (requestedModel != null && characterModels.Any(m => m.Name == requestedModel))
                                {
                                    Console.WriteLine($"Loading requested model: {requestedModel} from {prefix}");
                                    controller.LoadCharacter(prefix, requestedModel);
                                    characterLoaded = true;
                                    break;
                                }

                                // If no specific model was requested or it wasn't found, load the default
                                if (requestedModel == null)
                                {
                                    // Try to load ORC as default
                                    string modelToLoad = characterModels.Any(m => m.Name == "ORC")
                                        ? "ORC"
                                        : characterModels.First().Name;

                                    Console.WriteLine($"Loading default character model: {modelToLoad} from {prefix}");
                                    controller.LoadCharacter(prefix, modelToLoad);
                                    characterLoaded = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not load character file {prefix}: {ex.Message}");
                    }
                }
            }

            // If we couldn't load the requested model but found others, try to load any available model
            if (!characterLoaded && availableModels.Count > 0)
            {
                // If a specific model was requested but not found, show a message
                if (requestedModel != null)
                {
                    Console.WriteLine($"Warning: Requested model '{requestedModel}' not found. Available models:");
                    foreach (var (file, model) in availableModels)
                    {
                        Console.WriteLine($"  - {model} (in {file})");
                    }

                    // Load the first available model instead
                    var (firstFile, firstModel) = availableModels.First();
                    Console.WriteLine($"Loading {firstModel} from {firstFile} instead.");
                    controller.LoadCharacter(firstFile, firstModel);
                    characterLoaded = true;
                }
            }

            if (!characterLoaded)
            {
                Console.WriteLine("Warning: No valid character models could be loaded.");
            }
        }
    }
}
