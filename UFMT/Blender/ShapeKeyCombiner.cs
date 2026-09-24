using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UFMT.Core;

namespace UFMT.Blender
{
    internal static class ShapeKeyCombiner
    {
        private static string CombineShapeKeysScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PythonScripts", "Blender_CombineShapeKeys.py");
        internal static async Task<bool> CombineShapeKeys(string fbxFilePath)
        {
            if (!File.Exists(CombineShapeKeysScript))
            {
                Log.Error($"Failed to find python script! \"{CombineShapeKeysScript}\" does not exist or is not a python file!");
                return false;
            }

            if (!File.Exists(App.Settings.BlenderPath))
            {
                Log.Error($"Failed to find blender-launcher.exe! \"{App.Settings.BlenderPath}\" does not exist or is not a blender executable file!");
                return false;
            }

            if (!File.Exists(fbxFilePath))
            {
                Log.Error($"Failed to find character part head fbx! \"{fbxFilePath}\" does not exist or is not an .fbx file!");
                return false;
            }
            Console.WriteLine($"Combining shape keys for {Path.GetFileNameWithoutExtension(fbxFilePath)}");
            ProcessStartInfo psi = new ProcessStartInfo(App.Settings.BlenderPath, $"-b --python \"{CombineShapeKeysScript}\" -- \"{fbxFilePath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process blender = Process.Start(psi))
            {
                string stdout = await blender.StandardOutput.ReadToEndAsync();
                string stderr = await blender.StandardError.ReadToEndAsync();
                await blender.WaitForExitAsync();

                if (blender.ExitCode != 0)
                {
                    Log.Error($"Blender shape key combination failed with exit code {blender.ExitCode}:\n{stderr}");
                    return false;
                }

                if (!File.Exists(fbxFilePath))
                {
                    Log.Error($"Failed to combine shape keys for {Path.GetFileName(fbxFilePath)}.");
                    Log.Error("Make sure you are using the correct Blender version and have all required extensions enabled!");
                    return false;
                }
            }

            Log.Success($"Successfully combined shape keys for {fbxFilePath}");
            return true;
        }
    }
}
