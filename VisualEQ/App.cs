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
using VisualEQ.Views;
using static System.Console;

namespace VisualEQ
{
    internal class App
    {
        static void Main(string[] args)
        {
            try
            {
                // Get the zone name from arguments
                if (args.Length < 1)
                {
                    throw new Exception("Zone name is required. Usage: dotnet run <zone_name> [model_name]");
                }
                string zoneName = args[0];
                Console.WriteLine($"Loading {zoneName}");

                // Get the model name from arguments if provided
                string modelName = args.Length >= 2 ? args[1] : null;
                if (modelName != null)
                {
                    Console.WriteLine($"Will try to load character model: {modelName}");
                }

                // Add a debug flag for listing available models without loading any
                bool listModelsOnly = args.Length >= 2 && args[1].ToLower() == "--list-models";

			var controller = new Controller();
                // Set up circular reference so they can access each other
                controller.Engine.Controller = controller;

                // Load the zone
                controller.LoadZone(zoneName);

                // List or load character models
                if (listModelsOnly)
                {
                    // Just list available models without loading any
                    ListAvailableModels(zoneName);
                    // Exit after listing models
                    return;
                }
                else
                {
                    // Regular loading with selected or default model
                    LoadCharacters(controller, zoneName, modelName);
                }

                // Add views
			controller.AddView(new StatusView(controller));
                controller.AddView(new ModelEditorView(controller));

                // Add TeleportView with exception handling
                try
                {
                    controller.AddView(new TeleportView(controller));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not add TeleportView: {ex.Message}");
                }

			controller.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting application: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // New method to list all available models without loading them
        private static void ListAvailableModels(string zoneName)
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
                string characterPath = $"../ConverterApp/{prefix}_oes.zip";

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
                string characterPath = $"../ConverterApp/{prefix}_oes.zip";

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
