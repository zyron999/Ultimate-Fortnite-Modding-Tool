using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UFMT.Core;
using UFMT.FnAssets;
using UFMT.UI;
using UFMT.UnrealEngine;

namespace UFMT.FnAssetsLogic
{
    internal static class PickaxeValidator
    {
        internal static bool ValidateAfterPathChange(string currentPickaxeFolderPath, PickaxeData currentPickaxe)
        {
            if (currentPickaxe == null)
            {
                Log.Error("Current pickaxe was null when trying to validate it!");
                return false;
            }

            if (currentPickaxeFolderPath == string.Empty)
            {
                Log.Error("Current pickaxe path is empty!");
                return false;
            }

            if (!Directory.Exists(currentPickaxeFolderPath))
            {
                Log.Error($"\"{currentPickaxeFolderPath}\" does not exist or is not a directory!");
                return false;
            }

            string codename = Path.GetFileName(currentPickaxeFolderPath);
            if (codename.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            {
                Log.Error($"{codename} contains invalid characters; only basic English letters (A-Z), numbers, and underscores are allowed.");
                return false;
            }

            string sourcePath = Path.Combine(currentPickaxeFolderPath, "Source");
            if (!Directory.Exists(sourcePath))
            {
                Log.Error($"Cannot find Source folder inside \"{currentPickaxeFolderPath}\"");
                return false;
            }

            string meshesPath = Path.Combine(sourcePath, "Meshes");
            if (!Directory.Exists(meshesPath))
            {
                Log.Error($"Cannot find Meshes folder inside \"{sourcePath}\"");
                return false;
            }

            string physicsPath = Path.Combine(sourcePath, "Physics");
            if (!Directory.Exists(physicsPath)) 
            {
                Directory.CreateDirectory(physicsPath);
                Log.Warning($"Created missing Physics folder inside {sourcePath}"); // Continue with a warning since physics aren't mandatory
            } 

            string texturesPath = Path.Combine(sourcePath, "Textures");
            if (!Directory.Exists(texturesPath))
            {
                Log.Error($"Cannot find Textures folder inside \"{sourcePath}\"");
                return false;
            }

            string soundsPath = Path.Combine(sourcePath, "Sounds");
            if (!Directory.Exists(soundsPath))
            {
                Log.Error($"Cannot find Sounds folder inside \"{sourcePath}\"");
                return false;
            }

            currentPickaxe.Path = currentPickaxeFolderPath;
            currentPickaxe.SourcePath = sourcePath;
            currentPickaxe.MeshesPath = meshesPath;
            currentPickaxe.PhysicsPath = physicsPath;
            currentPickaxe.TexturesPath = texturesPath;
            currentPickaxe.SoundsPath = soundsPath;
            currentPickaxe.Codename = codename;
            return true;
        }

        internal static bool ValidateBeforeExportProcess(string uePickaxesPackagePath, string ueProjectPath, string ueExecutablePath, string name, string description,
        string rarity, string[] validRarities, UeVersion ueVersion, FnVersion fnVersion)
        {
            if (!uePickaxesPackagePath.StartsWith("/Game/"))
            {
                Log.Error($"\"{uePickaxesPackagePath}\" is not a valid Unreal package path, it must start with /Game/");
                return false;
            }

            if (!File.Exists(ueProjectPath))
            {
                Log.Error($"\"{ueProjectPath}\" does not exist or is not a valid .uproject file!");
                return false;
            }

            if (!ueProjectPath.EndsWith(".uproject"))
            {
                Log.Error($"\"{ueProjectPath}\" is not a .uproject file!");
                return false;
            }

            if (!File.Exists(ueExecutablePath))
            {
                Log.Error($"\"{ueExecutablePath}\" does not exist or is not a valid .exe file!");
                return false;
            }

            if (!ueExecutablePath.EndsWith(".exe"))
            {
                Log.Error($"\"{ueExecutablePath}\" is not an .exe file!");
                return false;
            }

            if (name == string.Empty)
            {
                Log.Error("Pickaxe name cannot be empty!");
                return false;
            }

            if (description == string.Empty)
            {
                Log.Error("Pickaxe description cannot be empty!");
                return false;
            }

            if (!validRarities.Contains(rarity))
            {
                Log.Error($"{rarity} is not a valid rarity!"); // If the user loads a JSON with unrecognized rarity? idk this check is just in case
                return false;
            }

            if (fnVersion == null)
            {
                Log.Error($"Fortnite version was null!");
                return false;
            }

            if (ueVersion == null)
            {
                Log.Error("Unreal Engine version was null!");
            }

            return true;
        }

        internal static bool ValidateAfterUeProcess(string ueProjectPath, string uePickaxesOsPath, string codename, string[] diffuseTextures, string[] maskTextures,
        string[] normalTextures, string[] specularTextures, List<string> materialNames, string mesh, string smallIcon, string largeIcon, List<string> allSoundWaves,
        string pid, string cookedAssetsPath, bool validateAfterCook = false)
        {
            // if a pickaxe had none material, don't check if it exists because ue materials cannot be none since that's a keyword
            if (materialNames.Contains("None")) materialNames.Remove("None");
            if (materialNames.Contains("none")) materialNames.Remove("none");
            string contentCurrentPickaxePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", uePickaxesOsPath, codename);
            string cookedCurrentPickaxePath = Path.Combine(cookedAssetsPath, uePickaxesOsPath, codename);
            if (!Path.Exists(contentCurrentPickaxePath))
            {
                Log.Error($"{contentCurrentPickaxePath} does not exist!");
                return false;
            }
            if (!Path.Exists(cookedCurrentPickaxePath) && validateAfterCook)
            {
                Log.Error($"{cookedCurrentPickaxePath} does not exist!");
                return false;
            }
            string ueSearchPath = validateAfterCook ? cookedCurrentPickaxePath : contentCurrentPickaxePath;

            if (!FindUeAssetFiles(Path.Combine(ueSearchPath, "Meshes"), mesh, validateAfterCook)) return false;
            if (materialNames.Any(mat => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Materials"), mat, validateAfterCook))) return false;
            if (diffuseTextures.Any(tex => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(tex), validateAfterCook))) return false;
            if (maskTextures.Any(tex => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(tex), validateAfterCook))) return false;
            if (normalTextures.Any(tex => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(tex), validateAfterCook))) return false;
            if (specularTextures.Any(tex => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(tex), validateAfterCook))) return false;
            if (allSoundWaves.Any(sound => !FindUeAssetFiles(Path.Combine(ueSearchPath, "Sounds"), Path.GetFileName(sound), validateAfterCook))) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(smallIcon), validateAfterCook)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(ueSearchPath, "Textures"), Path.GetFileName(largeIcon), validateAfterCook)) return false;

            if (!FindUeAssetFiles(Path.Combine(ueSearchPath), pid, validateAfterCook)) return false;
            return true;
        }

        internal static bool ValidateAfterUeCook(string cookedCurrentPickaxePath, string codename, string smallIcon, string largeIcon, string eid, bool usesIntro)
        {
            if (!Path.Exists(cookedCurrentPickaxePath))
            {
                Log.Error($"{cookedCurrentPickaxePath} does not exist!");
                return false;
            }
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "Animations"), $"Pickaxe_{codename}_CMM", true)) return false;
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "Animations"), $"Pickaxe_{codename}_CMf", true)) return false;
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "Sound"), $"{codename}_Sound_Loop", true)) return false;
            if (usesIntro && !FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "Sound"), $"{codename}_Sound_Intro", true)) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "UI"), $"T-Icon-Pickaxes-E-{codename}", true)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath, "UI"), $"T-Icon-Pickaxes-E-{codename}-L", true)) return false;

            if (!FindUeAssetFiles(Path.Combine(cookedCurrentPickaxePath), eid, true)) return false;
            return true;
        }

        private static bool FindUeAssetFiles(string cookedAssetPath, string fileName, bool checkForUexpFiles)
        {
            string uassetFilePath = Path.Combine(cookedAssetPath, Path.ChangeExtension(fileName, ".uasset"));
            string uexpFilePath = Path.ChangeExtension(uassetFilePath, ".uexp");
            if (!File.Exists(uassetFilePath))
            {
                Log.Error($"Can't find {uassetFilePath}");
                return false;
            }
            if (!File.Exists(uexpFilePath) && checkForUexpFiles)
            {
                Log.Error($"Can't find {uexpFilePath}");
                return false;
            }
            return true;
        }
    }
}