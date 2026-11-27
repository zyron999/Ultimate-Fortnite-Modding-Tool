using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UAssetAPI;
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
                string ueProjectFolderPath = Path.GetDirectoryName(ueProjectPath);
                Console.WriteLine("Adding and replacing required assets in Unreal Engine...");
                string fakeCIDTemplatePath = Path.Combine(ueProjectFolderPath, "Content", "CID_Template.uasset");
                string BaseMeshSkeletonPath = Path.Combine(ueProjectFolderPath, "Content",
                "Characters", "Player", "Male", "Male_Avg_Base", "Fortnite_M_Avg_Player_Skeleton.uasset");
                string BaseMeshPath = Path.Combine(ueProjectFolderPath, "Content",
                "Characters", "Player", "Male", "Male_Avg_Base", "Fortnite_M_Avg_Player.uasset");
                string baseHeadPath = Path.Combine(ueProjectFolderPath, ueBaseHeadPath);
                string mediumLodSettingsFilePath = Path.Combine(ueProjectFolderPath, "Content", "Characters", "Player", "Common", "LODSettings", "Medium_Player_LODSettings.uasset");
                string accessoriesSkeletonFilePath = Path.Combine(ueProjectFolderPath, "Content", "Accessories", "Accessories_Skeleton_Basic.uasset");
                string baseTailSkeletonFilePath = Path.Combine(ueProjectFolderPath, "Content", "Accessories", "FORT_Tails", "Common",
                "Fortnite_Base_Tail_Skeleton.uasset");
                string baseTailAnimBpFilePath = Path.Combine(ueProjectFolderPath, "Content", "Accessories", "FORT_Tails", "Common",
                "Fortnite_Base_Tail_AnimBP.uasset");
                string skMeleeSkeletonFilePath = Path.Combine(ueProjectFolderPath, "Content", "Weapons", "FORT_Melee", "Meshes", "SK_Melee_Skeleton.uasset");

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
                await AddOrReplaceUeAsset(baseTailAnimBpFilePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Fortnite_Base_Tail_AnimBP.uasset"));
                await AddOrReplaceUeAsset(skMeleeSkeletonFilePath, TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "SK_Melee_Skeleton.uasset"));

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
