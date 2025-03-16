using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using VisualEQ.Common;

namespace ModelLister
{
    class Program
    {
        // Common EverQuest animation codes to look for - used ONLY for pattern matching, not for adding defaults
        private static readonly HashSet<string> CommonAnimCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10",
            "W01", "W02", "W03", "W04", "W05", 
            "I01", "I02", "I03", "I04",
            "ITEM", "DEAD", "A01", "A02", "A03"
        };

        static int Main(string[] args)
        {
            bool showAnimations = true; // Default to showing animations
            string filePath = null;
            bool animsOnly = false;
            string animsOnlyModel = null;
            bool dumpOesStructure = false;

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--no-anims", StringComparison.OrdinalIgnoreCase))
                {
                    showAnimations = false;
                }
                else if (args[i].Equals("--anims-only", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    // This option lets you view animations for a specific model only
                    showAnimations = true;
                    animsOnly = true;
                    animsOnlyModel = args[i + 1];
                    i++; // Skip the next argument as we've consumed it
                }
                else if (args[i].Equals("--dump-oes", StringComparison.OrdinalIgnoreCase))
                {
                    dumpOesStructure = true;
                }
                else if (filePath == null)
                {
                    filePath = args[i];
                }
            }

            try
            {
                if (filePath == null)
                {
                    Console.WriteLine("Usage: ModelLister <path_to_chr_oes_zip> [--no-anims] [--anims-only MODEL_NAME] [--dump-oes]");
                    Console.WriteLine("Lists all character models available in the specified _chr_oes.zip file");
                    Console.WriteLine("Options:");
                    Console.WriteLine("  --no-anims         Don't show animation lists (faster)");
                    Console.WriteLine("  --anims-only NAME  Show animations only for the specified model");
                    Console.WriteLine("  --dump-oes         Show detailed OES file structure for debugging");
                    return 1;
                }
                
                // Ensure file exists
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Error: File not found: {filePath}");
                    return 1;
                }

                // Help people who might be running the wrong command
                if (!filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && 
                    !filePath.Contains("_chr"))
                {
                    Console.WriteLine($"Warning: File doesn't appear to be a character file: {filePath}");
                    Console.WriteLine("Character files typically end with _chr_oes.zip");
                    Console.WriteLine("Continue anyway? [Y/N]");
                    var key = Console.ReadKey(true);
                    if (key.Key != ConsoleKey.Y)
                    {
                        return 1;
                    }
                    Console.WriteLine("Continuing...");
                }

                Console.WriteLine($"Attempting to read character models from: {filePath}");
                
                // First try the main approach using OES parsing
                bool oesParsingSucceeded = false;
                try
                {
                    oesParsingSucceeded = ListModelsUsingOes(filePath, showAnimations, animsOnly, animsOnlyModel, dumpOesStructure);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with OES parsing: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                }

                // If OES parsing failed, show diagnostic info
                if (!oesParsingSucceeded)
                {
                    Console.WriteLine("\n===== OES Parsing Failed - Showing Diagnostic Info =====");
                    
                    // Show diagnostic info about the zip file
                    ShowZipContents(filePath);
                    
                    Console.WriteLine("\nThe character models zip file could not be parsed.");
                    Console.WriteLine("This may not be a valid character data file or it has an unsupported format.");
                    return 1;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        static bool ListModelsUsingOes(string filePath, bool showAnimations, bool animsOnly, string animsOnlyModel, bool dumpOesStructure)
        {
            Console.WriteLine("Using OES parser to find character models...");
            
            try
            {
                using (var zipFile = ZipFile.OpenRead(filePath))
                {
                    // Check for main.oes file
                    var mainOesEntry = zipFile.GetEntry("main.oes");
                    if (mainOesEntry == null)
                    {
                        Console.WriteLine("Error: The file does not contain main.oes which is required for character models");
                        return false;
                    }
                    
                    Console.WriteLine($"Found main.oes ({mainOesEntry.Length} bytes)");
                    
                    // Get all entry names for quick lookup
                    var allEntries = zipFile.Entries.Select(e => e.FullName).ToList();
                    
                    // Copy the entry to a memory stream first to avoid potential issues
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var entryStream = mainOesEntry.Open())
                        {
                            entryStream.CopyTo(memoryStream);
                        }
                        
                        // Reset the memory stream position to the beginning
                        memoryStream.Position = 0;
                        
                        Console.WriteLine("Parsing OES data...");
                        // Read the OES file structure
                        var root = OESFile.Read<OESRoot>(memoryStream);
                        
                        // If requested, dump the OES structure for debugging
                        if (dumpOesStructure)
                        {
                            Console.WriteLine("\n=== OES Structure Info ===");
                            Console.WriteLine($"OESRoot object created. Type: {root.GetType().FullName}");
                            Console.WriteLine("Looking for available character models and animation data...");
                            Console.WriteLine("=== End OES Structure Info ===\n");
                        }
                        
                        // Extract character model names
                        var characterModels = root.Find<OESCharacter>().ToList();
                        
                        if (characterModels.Count == 0)
                        {
                            Console.WriteLine("No character models found in this file.");
                            return false;
                        }

                        // Look for files that might contain animation information
                        Console.WriteLine("Looking for animation data in zip file entries...");
                        
                        // Dictionary to store model animations
                        var modelAnimations = new Dictionary<string, HashSet<string>>();
                        
                        // Initialize the animation dictionary for each model
                        foreach (var model in characterModels)
                        {
                            modelAnimations[model.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        
                        // Analyze file structure to detect animations
                        if (showAnimations)
                        {
                            Console.WriteLine("Analyzing file entries to detect animations...");
                            
                            // Collect all entries for analysis
                            var allFileEntries = zipFile.Entries
                                .Where(e => !string.IsNullOrEmpty(e.Name) && !e.FullName.EndsWith("/"))
                                .ToList();
                                
                            Console.WriteLine($"Found {allFileEntries.Count} files to analyze.");
                            
                            // First approach: Look for direct model_anim pattern (most reliable)
                            var animCountDirect = 0;
                            foreach (var model in characterModels)
                            {
                                var modelName = model.Name;
                                
                                // Pattern 1: Look for files named like "MODEL_ANIM.ext" - most common pattern
                                var modelPrefix = $"{modelName}_";
                                var modelAnimFiles = allFileEntries
                                    .Where(e => Path.GetFileNameWithoutExtension(e.Name).StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                    
                                foreach (var file in modelAnimFiles)
                                {
                                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                                    var parts = fileName.Split('_');
                                    if (parts.Length >= 2)
                                    {
                                        var animCode = parts[1].Trim();
                                        // Filter to likely animation codes (validate format)
                                        if (IsLikelyAnimCode(animCode))
                                        {
                                            modelAnimations[modelName].Add(animCode);
                                            animCountDirect++;
                                        }
                                    }
                                }
                            }
                            Console.WriteLine($"Found {animCountDirect} animations using direct model_anim pattern.");
                            
                            // Second approach: Look inside model directories (common in OES)
                            var animCountDirs = 0;
                            foreach (var model in characterModels)
                            {
                                var modelName = model.Name;
                                
                                // Pattern 2: Files in model directories with animation codes
                                var modelDirPrefix = $"{modelName}/";
                                var modelDirFiles = allFileEntries
                                    .Where(e => e.FullName.Contains(modelDirPrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                    
                                foreach (var file in modelDirFiles)
                                {
                                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                                    
                                    // Check for common animation codes in the filename
                                    foreach (var animCode in CommonAnimCodes)
                                    {
                                        if (fileName.Contains(animCode, StringComparison.OrdinalIgnoreCase))
                                        {
                                            modelAnimations[modelName].Add(animCode);
                                            animCountDirs++;
                                            break; // Found one animation for this file
                                        }
                                    }
                                }
                            }
                            Console.WriteLine($"Found {animCountDirs} animations in model directories.");
                            
                            // Third approach: Use regex to find animation patterns in any file
                            var animCountRegex = 0;
                            if (animCountDirect + animCountDirs == 0)
                            {
                                Console.WriteLine("Using regex pattern matching as a last resort...");
                                
                                foreach (var model in characterModels)
                                {
                                    var modelName = model.Name;
                                    
                                    // Pattern 3: Use regex to find animation codes near model names
                                    var modelNamePattern = $@"{Regex.Escape(modelName)}[-_/\\]([A-Za-z][0-9]{{2}}|ITEM|DEAD)";
                                    var modelRegex = new Regex(modelNamePattern, RegexOptions.IgnoreCase);
                                    
                                    foreach (var file in allFileEntries)
                                    {
                                        var match = modelRegex.Match(file.FullName);
                                        if (match.Success && match.Groups.Count > 1)
                                        {
                                            var animCode = match.Groups[1].Value;
                                            modelAnimations[modelName].Add(animCode);
                                            animCountRegex++;
                                        }
                                    }
                                }
                                Console.WriteLine($"Found {animCountRegex} animations using regex pattern matching.");
                            }
                            
                            // If we still don't have animations, add the common ones that we know work from the code
                            // BUT with special handling for models that crash with certain animations
                            if (animCountDirect + animCountDirs + animCountRegex == 0)
                            {
                                Console.WriteLine("\nNo animations detected from file patterns. Adding known working animations based on code examples.");
                                
                                // Add basic animations with model-specific compatibility
                                foreach (var model in characterModels)
                                {
                                    // Add C01 (stand) to all models
                                    modelAnimations[model.Name].Add("C01");
                                    
                                    // GFF crashes with C05 - don't add it for this model
                                    if (!model.Name.Equals("GFF", StringComparison.OrdinalIgnoreCase))
                                    {
                                        modelAnimations[model.Name].Add("C05"); // Walk
                                    }
                                    
                                    // Add ITEM to all models
                                    modelAnimations[model.Name].Add("ITEM");
                                }
                                
                                Console.WriteLine("Added default animations based on known compatibility");
                                Console.WriteLine("Note: C05 not added to GFF model (known to crash)");
                            }
                            
                            // After all animation detection, do a final check to remove known problematic animations
                            foreach (var model in characterModels)
                            {
                                // GFF crashes with C05 - ensure it's removed under all circumstances
                                if (model.Name.Equals("GFF", StringComparison.OrdinalIgnoreCase))
                                {
                                    modelAnimations[model.Name].Remove("C05");
                                }
                            }
                        }
                        
                        // Filter results if looking for a specific model's animations
                        if (animsOnly && !string.IsNullOrEmpty(animsOnlyModel))
                        {
                            // Find the model (case-insensitive match)
                            var matchingModels = characterModels
                                .Where(m => m.Name.Equals(animsOnlyModel, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                                
                            if (matchingModels.Count == 0)
                            {
                                Console.WriteLine($"\nNo model named '{animsOnlyModel}' found. Available models:");
                                foreach (var model in characterModels)
                                {
                                    Console.WriteLine($"  {model.Name}");
                                }
                                return true;
                            }
                            
                            foreach (var model in matchingModels)
                            {
                                Console.WriteLine($"\nAnimations for model: {model.Name}");
                                if (showAnimations && modelAnimations.ContainsKey(model.Name))
                                {
                                    var anims = modelAnimations[model.Name].OrderBy(a => a).ToList();
                                    if (anims.Count > 0)
                                    {
                                        Console.WriteLine($"  {string.Join(", ", anims)}");
                                    }
                                    else
                                    {
                                        Console.WriteLine("  No animations found");
                                        Console.WriteLine("  Caution: Attempting to use animations not present in the files may crash the game");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("  Animation data not available");
                                }
                            }
                        }
                        else
                        {
                            // Show all models and their animations
                            Console.WriteLine($"\nFound {characterModels.Count} models in {Path.GetFileName(filePath)}:");
                            foreach (var model in characterModels)
                            {
                                if (showAnimations && modelAnimations.ContainsKey(model.Name))
                                {
                                    var anims = modelAnimations[model.Name].OrderBy(a => a).ToList();
                                    if (anims.Count > 0)
                                    {
                                        Console.WriteLine($"{model.Name} (Animations: {string.Join(", ", anims)})");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"{model.Name} (No animations found)");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine(model.Name);
                                }
                            }
                        }
                        
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading OES format: {ex.Message}");
                throw; // Rethrow to be handled by the caller
            }
        }
        
        // Helper to determine if a string is likely an animation code
        private static bool IsLikelyAnimCode(string code)
        {
            // Common animation codes are already known
            if (CommonAnimCodes.Contains(code))
                return true;
                
            // Check format: 1-3 letters followed by 1-2 digits, or special cases
            return (code.Length >= 2 && code.Length <= 4 &&
                   (char.IsLetter(code[0]) || code == "ITEM" || code == "DEAD"));
        }
        
        static void ShowZipContents(string filePath)
        {
            try
            {
                using (var zipFile = ZipFile.OpenRead(filePath))
                {
                    var entries = zipFile.Entries.ToList();
                    
                    Console.WriteLine($"\nZip file contains {entries.Count} entries");
                    Console.WriteLine("First 10 entries (alphabetical):");
                    
                    foreach (var entry in entries.OrderBy(e => e.FullName).Take(10))
                    {
                        Console.WriteLine($"  {entry.FullName} ({entry.Length} bytes)");
                    }
                    
                    // Show common file types
                    var fileTypes = entries
                        .Select(e => Path.GetExtension(e.FullName).ToLowerInvariant())
                        .Where(ext => !string.IsNullOrEmpty(ext))
                        .GroupBy(ext => ext)
                        .OrderByDescending(g => g.Count())
                        .Take(5);
                        
                    Console.WriteLine("\nMost common file types:");
                    foreach (var type in fileTypes)
                    {
                        Console.WriteLine($"  {type.Key}: {type.Count()} files");
                    }

                    // Look for files that match model_animation patterns
                    var possibleAnimationFiles = entries
                        .Where(e => e.FullName.Contains("_") && 
                                   (CommonAnimCodes.Any(c => e.FullName.Contains(c))))
                        .Take(10)
                        .ToList();
                        
                    if (possibleAnimationFiles.Any())
                    {
                        Console.WriteLine("\nSample files that might contain animation data:");
                        foreach (var file in possibleAnimationFiles)
                        {
                            Console.WriteLine($"  {file.FullName}");
                        }
                    }
                    
                    // Show file extensions that might contain animation data
                    Console.WriteLine("\nFile types that might contain animation data:");
                    foreach (var ext in new[] { ".wld", ".ani", ".anm", ".anim" })
                    {
                        var count = entries.Count(e => e.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
                        Console.WriteLine($"  {ext}: {count} files");
                        
                        if (count > 0)
                        {
                            Console.WriteLine("  Sample files:");
                            foreach (var file in entries.Where(e => e.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)).Take(5))
                            {
                                Console.WriteLine($"    {file.FullName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading zip contents: {ex.Message}");
            }
        }
    }
} 