using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UFMT.Core;
using UFMT.UI;

namespace UFMT.FnAssetsLogic
{
    internal static class EmoteValidator
    {
        internal static bool ValidateAfterPathChange(string currentEmoteFolderPath, EmoteData currentEmote)
        {
            if (currentEmote == null)
            {
                Log.Error("Current emote was null when trying to validate it!");
                return false;
            }

            if (currentEmoteFolderPath == string.Empty)
            {
                Log.Error("Current emote path is empty!");
                return false;
            }

            if (!Directory.Exists(currentEmoteFolderPath))
            {
                Log.Error($"\"{currentEmoteFolderPath}\" does not exist or is not a directory!");
                return false;
            }

            string codename = Path.GetFileName(currentEmoteFolderPath);
            if (codename.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            {
                Log.Error($"{codename} contains invalid characters; only basic English letters (A-Z), numbers, and underscores are allowed.");
                return false;
            }

            string sourcePath = Path.Combine(currentEmoteFolderPath, "Source");
            if (!Directory.Exists(sourcePath))
            {
                Log.Error($"Cannot find Source folder inside \"{currentEmoteFolderPath}\"");
                return false;
            }

            string animationsPath = Path.Combine(sourcePath, "Animations");
            if (!Directory.Exists(animationsPath))
            {
                Log.Error($"Cannot find Animations folder inside \"{sourcePath}\"");
                return false;
            }

            string iconsPath = Path.Combine(sourcePath, "Icons");
            if (!Directory.Exists(iconsPath))
            {
                Log.Error($"Cannot find Icons folder inside \"{sourcePath}\"");
                return false;
            }

            string soundPath = Path.Combine(sourcePath, "Sound");
            if (!Directory.Exists(soundPath))
            {
                Log.Error($"Cannot find Sound folder inside \"{sourcePath}\"");
                return false;
            }

            currentEmote.Path = currentEmoteFolderPath;
            currentEmote.SourcePath = sourcePath;
            currentEmote.AnimationsPath = animationsPath;
            currentEmote.IconsPath = iconsPath;
            currentEmote.SoundPath = soundPath;
            currentEmote.Codename = codename;
            return true;
        }

        internal static bool ValidateAfterUeImport(string ueProjectPath, string ueEmotesOsPath, string codename, string smallIcon, string largeIcon, string eid)
        {
            string contentCurrentEmotePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", ueEmotesOsPath, codename);
            if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath, "Animations"), $"Emote_{codename}_CMM", false)) return false;
            if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath, "Animations"), $"Emote_{codename}_CMf", false)) return false;
            if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath, "Sound"), $"{codename}_Sound", false)) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath, "UI"), $"T-Icon-Emotes-E-{codename}", false)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath, "UI"), $"T-Icon-Emotes-E-{codename}-L", false)) return false;

            if (!FindUeAssetFiles(Path.Combine(contentCurrentEmotePath), eid, false)) return false;
            return true;
        }

        internal static bool ValidateAfterUeCook(string cookedCurrentEmotePath, string codename, string smallIcon, string largeIcon, string eid)
        {
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath, "Animations"), $"Emote_{codename}_CMM", true)) return false;
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath, "Animations"), $"Emote_{codename}_CMf", true)) return false;
            if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath, "Sound"), $"{codename}_Sound", true)) return false;

            if (smallIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath, "UI"), $"T-Icon-Emotes-E-{codename}", true)) return false;
            if (largeIcon != string.Empty) if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath, "UI"), $"T-Icon-Emotes-E-{codename}-L", true)) return false;

            if (!FindUeAssetFiles(Path.Combine(cookedCurrentEmotePath), eid, true)) return false;
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
