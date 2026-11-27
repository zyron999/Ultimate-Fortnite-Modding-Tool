using System;
using System.IO;
using System.Linq;
using UFMT.Core;

namespace UFMT.FnAssetsLogic
{
    internal class PickaxeFolderScanner
    {
        internal static (bool success, string pskPath, string[] physicsAssetsJsonNames) GetMeshData(string meshesPath, string physicsPath)
        {
            try
            {
                string[] pskPaths = Directory.GetFiles(meshesPath, "*.psk");
                if (pskPaths.Length == 0)
                {
                    Log.Error($"No .psk files found inside {meshesPath}!");
                    return (false, null, null);
                }
                if (pskPaths.Length > 1)
                {
                    Log.Error($"Multiple .psk files found inside {meshesPath}, make sure there is only 1!");
                    return (false, null, null);
                }

                string[] physicsJsons = Directory.GetFiles(physicsPath, "*.json");

                return (true, pskPaths[0], physicsJsons.Select(json => Path.GetFileName(json)).ToArray());
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while trying to find pickaxe's character parts! {ex}");
                return (false, null, null);
            }
        }

        internal static (bool isValid, string[] impactSoundWaves, string[] readySoundWaves, string[] swingSoundWaves) FindSoundFiles(string soundsPath)
        {
            try
            {
                string impactSoundsPath = Path.Combine(soundsPath, "Impact");
                string readySoundsPath = Path.Combine(soundsPath, "Ready");
                string swingSoundsPath = Path.Combine(soundsPath, "Swing");

                if (!Directory.Exists(impactSoundsPath))
                {
                    Log.Error($"Cannot find Impact folder in {soundsPath}!");
                    return (false, null, null, null);
                }

                if (!Directory.Exists(readySoundsPath))
                {
                    Log.Error($"Cannot find Ready folder in {soundsPath}!");
                    return (false, null, null, null);
                }

                if (!Directory.Exists(swingSoundsPath))
                {
                    Log.Error($"Cannot find Swing folder in {soundsPath}!");
                    return (false, null, null, null);
                }

                string[] impactSoundWaves = Directory.GetFiles(impactSoundsPath, "*.wav").Select(wav => Path.GetFileName(wav)).ToArray();
                string[] readySoundWaves = Directory.GetFiles(readySoundsPath, "*.wav").Select(wav => Path.GetFileName(wav)).ToArray();
                string[] swingSoundWaves = Directory.GetFiles(swingSoundsPath, "*.wav").Select(wav => Path.GetFileName(wav)).ToArray();

                if (impactSoundWaves.Count() == 0)
                {
                    Log.Error($"'{impactSoundsPath}' does not have any .wav files");
                    return (false, null, null, null);
                }

                if (swingSoundWaves.Count() == 0)
                {
                    Log.Error($"'{swingSoundsPath}' does not have any .wav files");
                    return (false, null, null, null);
                }

                if (readySoundWaves.Count() == 0) Log.Warning($"'{readySoundsPath}' does not have any .wav files, swing sounds will be used for ready sounds instead");

                return (true, impactSoundWaves, readySoundWaves, swingSoundWaves);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while trying to find sound files for the pickaxe: {ex}");
                return (false, null, null, null);
            }
        }
    }
}
