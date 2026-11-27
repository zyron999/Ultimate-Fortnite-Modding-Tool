using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UFMT;
using UFMT.Blender;
using UFMT.Core;
using UFMT.FnAssets;

namespace UFMT.Blender
{
    internal static class FbxConverter
    {
        private static string PskConvertScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PythonScripts", "Blender_ConvertPsk.py");
        private static string PsaConvertScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PythonScripts", "Blender_ConvertPsa.py");
        private static string ProperSkeletonBlendPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "proper_fn_skeleton.blend");

        internal static async Task<bool> ConvertPskToFbx(string pskPath, string fbxFilePath)
        {
            string fbxFolderPath = Path.GetDirectoryName(fbxFilePath);
            Directory.CreateDirectory(fbxFolderPath);
            string[] fbxFiles = Directory.GetFiles(fbxFolderPath, "*.fbx");
            if (fbxFiles.Length > 1)
            {
                Log.Error($"More than 1 fbx files found in {fbxFolderPath}, make sure there is only 1 mesh per character part!");
                return false;
            }

            if (File.Exists(fbxFilePath)) 
            {
                Console.WriteLine($"{Path.GetFileName(fbxFilePath)} already exists, skipping {Path.GetFileName(pskPath)} fbx conversion...");
                return true;
            } 

            if (fbxFiles.Length == 1)
            {
                File.Move(fbxFiles[0], fbxFilePath);
                return true;
            }

            Console.WriteLine($"Converting {Path.GetFileName(pskPath)} to {Path.GetFileName(fbxFilePath)}...");
            if (!File.Exists(PskConvertScript))
            {
                Log.Error($"Failed to find Blender_ConvertPsk.py! \"{PskConvertScript}\" does not exist or is not a python file!");
                return false;
            }

            if (!File.Exists(App.Settings.BlenderPath))
            {
                Log.Error($"Failed to find blender-launcher.exe! \"{App.Settings.BlenderPath}\" does not exist or is not a blender exectuable file!");
                return false;
            }

            if (!File.Exists(pskPath))
            {
                Log.Error($"\"{pskPath}\" does not exist or is not a valid .psk file!");
                return false;
            }

            ProcessStartInfo psi = new ProcessStartInfo(App.Settings.BlenderPath, $"-b --python \"{PskConvertScript}\" -- \"{pskPath}\" \"{fbxFilePath}\"")
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
                    Log.Error($"Blender export failed with exit code {blender.ExitCode}:\n{stderr}");
                    return false;
                }

                if (!File.Exists(fbxFilePath))
                {
                    Log.Error($"Failed to convert {Path.GetFileName(pskPath)} to {Path.GetFileName(fbxFilePath)}.");
                    Log.Error("Make sure you are using the correct Blender version and have all the plugins properly installed!");
                    return false;
                }
            }

            Log.Success($"Successfully converted {Path.GetFileName(pskPath)} to {fbxFilePath}!");
            return true;
        }

        internal static async Task<bool> ConvertPsaToFbx(string psaFilePath, string fbxFileExportPath, bool allowOnlyOneFbx)
        {
            try
            {
                if (!File.Exists(psaFilePath))
                {
                    Log.Error($"\"{psaFilePath}\" does not exist or is not a valid .psa file!");
                    return false;
                }

                if (!File.Exists(PsaConvertScript))
                {
                    Log.Error($"Failed to find Blender_ConvertPsa.py! \"{PsaConvertScript}\" does not exist or is not a python file!");
                    return false;
                }

                if (!File.Exists(ProperSkeletonBlendPath))
                {
                    Log.Error($"Failed to find proper_fn_skeleton.blend! \"{ProperSkeletonBlendPath}\" does not exist or is not a python file!");
                    return false;
                }

                if (!File.Exists(App.Settings.BlenderPath))
                {
                    Log.Error($"Failed to find blender-launcher.exe! \"{App.Settings.BlenderPath}\" does not exist or is not a blender exectuable file!");
                    return false;
                }

                string fbxFolderPath = Path.GetDirectoryName(fbxFileExportPath);
                if (!Directory.Exists(fbxFolderPath)) Directory.CreateDirectory(fbxFolderPath);
                string[] fbxFiles = Directory.GetFiles(fbxFolderPath, "*.fbx");
                if (fbxFiles.Length > 1 && allowOnlyOneFbx)
                {
                    Log.Error($"More than 1 fbx files found in {fbxFolderPath}, make sure there is only 1 animation!");
                    return false;
                }
                else if (fbxFiles.Length == 1 && allowOnlyOneFbx)
                {
                    File.Move(fbxFiles[0], fbxFileExportPath);
                }
                else if (!File.Exists(fbxFileExportPath))
                {
                    Console.WriteLine($"Converting {Path.GetFileName(psaFilePath)} to {Path.GetFileName(fbxFileExportPath)}...");

                    string arguments = $"-b \"{ProperSkeletonBlendPath}\" --python \"{PsaConvertScript}\" -- \"{psaFilePath}\" \"{fbxFileExportPath}\"";

                    ProcessStartInfo psi = new ProcessStartInfo(App.Settings.BlenderPath, arguments)
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
                            Log.Error($"Blender export failed with exit code {blender.ExitCode}:\n{stderr}");
                            return false;
                        }
                        if (!File.Exists(fbxFileExportPath))
                        {
                            Log.Error($"Failed to convert {Path.GetFileName(psaFilePath)} to {Path.GetFileName(fbxFileExportPath)}");
                            Log.Error("Make sure you are using the correct Blender version and have all the plugins properly installed!");
                            return false;
                        }
                    }

                    Log.Success($"Successfully converted {Path.GetFileName(psaFilePath)} to {Path.GetFileName(fbxFileExportPath)}");
                }
                return true;
            }
            catch (Exception ex )
            {
                Log.Error($"An error occurred while trying to convert .psa to .fbx: {ex.Message}");
                return false;
            }
        }
    }
}
