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
        internal static async Task<bool> AddRequiredUeAssetsBeforeExport(string ueProjectPath, string ueBaseHeadPath, string cookedCodenamePath, string ueVersionNumber,
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
                string accessoriesFolderPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "Accessories");
                string accessoriesSkeletonFilePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "Accessories", "Accessories_Skeleton_Basic.uasset");
                string baseTailSkeletonFilePath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Content", "Accessories", "FORT_Tails", "Common",
                "Fortnite_Base_Tail_Skeleton.uasset");

                await AddOrReplaceUeAsset(fakeCIDTemplatePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "FakeCID.uasset"));
                await AddOrReplaceUeAsset(BaseMeshSkeletonPath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "BaseMeshSkeleton.uasset"));
                await AddOrReplaceUeAsset(BaseMeshPath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "BaseMesh.uasset"));
                if (Directory.Exists(cookedCodenamePath)) Directory.Delete(cookedCodenamePath, true);
                await AddOrReplaceUeAsset(mediumLodSettingsFilePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Medium_Player_LODSettings.uasset"));
                foreach (string fileName in baseHeadFileNames)
                {
                    string filePath = Path.Combine(baseHeadPath, $"{fileName}");
                    await AddOrReplaceUeAsset(filePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", fileName));
                }
                await AddOrReplaceUeAsset(accessoriesSkeletonFilePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Accessories_Skeleton_Basic.uasset"));
                await AddOrReplaceUeAsset(baseTailSkeletonFilePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Fortnite_Base_Tail_Skeleton.uasset"));

                if (Directory.Exists(pluginPath)) Directory.Delete(pluginPath, true);
                Console.WriteLine($"Adding/Replacing {Path.GetFileNameWithoutExtension(pluginPath)}...");
                await Task.Run(() => ZipFile.ExtractToDirectory(physicsImporterPath, pluginPath));
                Console.WriteLine($"Replaced/Added {Path.GetFileNameWithoutExtension(physicsImporterPath)}");

                Log.Success($"Succesfully Added/Replaced required assets in UE");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while trying to add and replace required assets in Unreal Engine. {ex.Message}");
                return false;
            }
        }

        internal static async Task AddOrReplaceUeAsset(string filePath, byte[] fileInBytes)
        {
            string fileFolderPath = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);
            if (!Directory.Exists(fileFolderPath)) Directory.CreateDirectory(fileFolderPath);
            Console.WriteLine($"Adding/Replacing {Path.GetFileNameWithoutExtension(filePath)}...");
            await File.WriteAllBytesAsync(filePath, fileInBytes);
            Console.WriteLine($"Replaced/Added {fileName}");
        }
    }
}
