using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UFMT.Core;

namespace UFMT.UnrealEngine
{
    internal static class UnrealDependencySetup
    {
        internal static async Task<bool> AddRequiredUeAssetsBeforeExport(string ueProjectPath, string ueBaseHeadPath, string cookedCodenamePath, string UeVersionNumber, 
        string[] baseHeadFileNames, string pluginPath, string physicsImporterPath)
        {
            try
            {
                Console.WriteLine("Adding and replacing required assets in Unreal Engine...");
                string fakeCIDTemplatePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "CID_Template.uasset");
                string BaseMeshSkeletonPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content",
                "Characters", "Player", "Male", "Male_Avg_Base", "Fortnite_M_Avg_Player_Skeleton.uasset");
                string BaseMeshPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content",
                "Characters", "Player", "Male", "Male_Avg_Base", "Fortnite_M_Avg_Player.uasset");
                string baseHeadPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), ueBaseHeadPath);
                string mediumLodSettingsFolderPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "Characters", "Player", "Common", "LODSettings");
                string mediumLodSettingsFilePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "Characters", "Player", "Common", "LODSettings", "Medium_Player_LODSettings.uasset");

                await File.WriteAllBytesAsync(fakeCIDTemplatePath, TemplateLoader.GetEmbeddedFile(UeVersionNumber, "RawUeAssets", "FakeCID.uasset"));
                if (!Directory.Exists(BaseMeshSkeletonPath)) Directory.CreateDirectory(Path.GetDirectoryName(BaseMeshSkeletonPath));
                await File.WriteAllBytesAsync(BaseMeshSkeletonPath, TemplateLoader.GetEmbeddedFile(UeVersionNumber, "RawUeAssets", "BaseMeshSkeleton.uasset"));
                await File.WriteAllBytesAsync(BaseMeshPath, TemplateLoader.GetEmbeddedFile(UeVersionNumber, "RawUeAssets", "BaseMesh.uasset"));
                if (Directory.Exists(cookedCodenamePath)) Directory.Delete(cookedCodenamePath, true);
                if (!Directory.Exists(baseHeadPath))
                {
                    Directory.CreateDirectory(baseHeadPath);
                }
                if (!Directory.Exists(mediumLodSettingsFolderPath))
                {
                    Directory.CreateDirectory(mediumLodSettingsFolderPath);
                }
                await File.WriteAllBytesAsync(mediumLodSettingsFilePath, TemplateLoader.GetEmbeddedFile(UeVersionNumber, "RawUeAssets", "Medium_Player_LODSettings.uasset"));

                foreach (string fileName in baseHeadFileNames)
                {
                    string filePath = Path.Combine(baseHeadPath, $"{fileName}");
                    await File.WriteAllBytesAsync(filePath, TemplateLoader.GetEmbeddedFile(UeVersionNumber, "RawUeAssets", fileName));
                }

                if (Directory.Exists(pluginPath)) Directory.Delete(pluginPath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(physicsImporterPath, pluginPath));
                Log.Success($"Succesfully Added/Replaced required assets in ue");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while trying to add and replace required assets in Unreal Engine. {ex.Message}");
                return false;
            }
        }
    }
}
