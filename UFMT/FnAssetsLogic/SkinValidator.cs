using ABI.Windows.ApplicationModel.Activation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UFMT.Core;
using UFMT.UI;
using static System.Net.Mime.MediaTypeNames;

namespace UFMT.FnAssetsLogic
{
    internal static class SkinValidator
    {
        internal static bool ValidateAfterPathChange(string currentSkinFolderPath, SkinData currentSkin)
        {
            if (currentSkinFolderPath == string.Empty)
            {
                Log.Error("The Current skin path is empty!");
                return false;
            }
            if (!Directory.Exists(currentSkinFolderPath))
            {
                Log.Error($"\"{currentSkinFolderPath}\" doesn't exist!");
                return false;
            }
            string sourcePath = Path.Combine(currentSkinFolderPath, "Source");
            if (!Directory.Exists(sourcePath))
            {
                Log.Error($"Cannot find Source folder inside \"{currentSkinFolderPath}\"");
                return false;
            }
            string meshesPath = Path.Combine(sourcePath, "Meshes");
            if (!Directory.Exists(meshesPath))
            {
                Log.Error($"Cannot find Meshes folder inside \"{sourcePath}\"");
                return false;
            }
            string texturesPath = Path.Combine(sourcePath, "Textures");
            if (!Directory.Exists(texturesPath))
            {
                Log.Error($"Cannot find Textures folder inside \"{sourcePath}\"");
                return false;
            }
            string lobbyAnimationFolderPath = Path.Combine(sourcePath, "Lobby_Animation");
            if (!Directory.Exists(lobbyAnimationFolderPath))
            {
                Log.Error($"Cannot find Lobby_Animation folder inside \"{sourcePath}\"");
                return false;
            }
            string physicsPath = Path.Combine(sourcePath, "Physics");
            if (!Directory.Exists(physicsPath))
            {
                Log.Error($"Cannot find Physics folder inside \"{sourcePath}\"");
                return false;
            }

            currentSkin.Path = currentSkinFolderPath;
            currentSkin.SourcePath = sourcePath;
            currentSkin.MeshesPath = meshesPath;
            currentSkin.TexturesPath = texturesPath;
            currentSkin.LobbyAnimationFolderPath = lobbyAnimationFolderPath;
            currentSkin.PhysicsPath = physicsPath;
            return true;
        }

        internal static bool ValidateBeforeExport(string ueVersion, string gender, string name, string description, string cid, string ueSkinsPackagePath, 
        string ueProjectPath, string ueExecutablePath)
        {
            if (string.IsNullOrEmpty(ueVersion))
            {
                Log.Error($"No unreal engine selected! Make sure you selected the correct ue version in the settings!");
                return false;
            }

            if (!ueSkinsPackagePath.StartsWith("/Game/"))
            {
                Log.Error($"\"{ueSkinsPackagePath}\" is not a valid Unreal package path, it must start with /Game/");
                return false;
            }

            if (string.IsNullOrEmpty(ueProjectPath)) 
            {
                Log.Error("Unreal Engine Project path is empty!"); 
                return false; 
            }
            if (!File.Exists(ueProjectPath) || Path.GetExtension(ueProjectPath) != ".uproject") 
            { 
                Log.Error($"{ueProjectPath} does not exist or is not a valid .uproj file!"); 
                return false; 
            }

            if (!File.Exists(ueExecutablePath) || Path.GetExtension(ueExecutablePath) != ".exe")
            {
                Log.Error($"{ueExecutablePath} does not exist or is not a valid unreal executable file!");
                return false;
            }

            if (string.IsNullOrEmpty(gender))
            {
                Log.Error($"Skin's gender is unspecified!");
                return false;
            }
            if (string.IsNullOrEmpty(name))
            {
                Log.Error($"Skin's name cannot be empty!");
                return false;
            }
            if (string.IsNullOrEmpty(description))
            {
                Log.Error($"Skin's description cannot be empty!");
                return false;
            }
            if (string.IsNullOrEmpty(cid))
            {
                Log.Error($"Skin's CID cannot be empty!");
                return false;
            }

            return true;
        }

        internal static bool ValidateAfterUeImport(string ueProjectPath, string ueSkinsOsPath, string codename, List<string> diffuseTextures, List<string> maskTextures,
        List<string> normalTextures, List<string> specularTextures, List<string> materials, List<string> meshes, string smallIcon, string largeIcon, string lobbyAnimation, string cid)
        {
            string contentCurrentSkinPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", ueSkinsOsPath, codename);
            if (!Path.Exists(contentCurrentSkinPath))
            {
                Log.Error($"{contentCurrentSkinPath} does not exist!");
                return false;
            }
            if (meshes.Any(mesh => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Meshes"), mesh, false))) return false;
            if (materials.Any(mat => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Materials"), mat, false))) return false;
            if (diffuseTextures.Any(tex => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(tex), false))) return false;
            if (maskTextures.Any(tex => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(tex), false))) return false;
            if (normalTextures.Any(tex => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(tex), false))) return false;
            if (specularTextures.Any(tex => !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(tex), false))) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(smallIcon), false)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Textures"), Path.GetFileName(largeIcon), false)) return false;

            if (lobbyAnimation != string.Empty && !FindUeAssetFiles(Path.Combine(contentCurrentSkinPath, "Animations"), lobbyAnimation, false)) return false;

            if (!FindUeAssetFiles(Path.Combine(contentCurrentSkinPath), cid, false)) return false;
            return true;
        }

        internal static bool ValidateAfterUeCook(string cookedCurrentSkinPath, string codename, List<string> diffuseTextures, List<string> maskTextures,
        List<string> normalTextures, List<string> specularTextures, List<string> materials, List<string> meshes, string smallIcon, string largeIcon, string lobbyAnimation, string cid)
        {
            if (!Path.Exists(cookedCurrentSkinPath))
            {
                Log.Error($"{cookedCurrentSkinPath} does not exist!");
                return false;
            }
            if (meshes.Any(mesh => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Meshes"), mesh, true))) return false;
            if (materials.Any(mat => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Materials"), mat, true))) return false;
            if (diffuseTextures.Any(tex => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(tex), true))) return false;
            if (maskTextures.Any(tex => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(tex), true))) return false;
            if (normalTextures.Any(tex => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(tex), true))) return false;
            if (specularTextures.Any(tex => !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(tex), true))) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(smallIcon), true)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Textures"), Path.GetFileName(largeIcon), true)) return false;

            if (lobbyAnimation != string.Empty && !FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath, "Animations"), lobbyAnimation, true)) return false;

            if (!FindUeAssetFiles(Path.Combine(cookedCurrentSkinPath), cid, true)) return false;
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
