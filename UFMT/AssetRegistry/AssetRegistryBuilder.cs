using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UFMT.Core;
using UFMT.Helper;

namespace UFMT.AssetRegistry
{
    internal static class AssetRegistryBuilder
    {
        internal static void CreateAssetRegistry(string cookedAssetsPath, string ueVersionNumber, string outputFnGamePath, 
        string ueSkinsPackagePath, string ueEmotesPackagePath, string uePickaxesPackagePath, string currentCosmeticPath)
        {
            Console.WriteLine("Creating AssetRegistry.bin!");
            // Just in case the user has the project folder named differently than the .uproject
            string cookedSkinsPath = Path.Combine(cookedAssetsPath, ueSkinsPackagePath.Substring(6, ueSkinsPackagePath.Length - 6).Replace("/", "\\"));
            string cookedEmotesPath = Path.Combine(cookedAssetsPath, ueEmotesPackagePath.Substring(6, ueEmotesPackagePath.Length - 6).Replace("/", "\\"));
            string cookedPickaxesPath = Path.Combine(cookedAssetsPath, uePickaxesPackagePath.Substring(6, uePickaxesPackagePath.Length - 6).Replace("/", "\\"));

            string[] customSkinFolders = [];
            if (Path.Exists(cookedSkinsPath)) customSkinFolders = Directory.GetDirectories(Path.Combine(cookedSkinsPath));
            string[] customEmoteFolders = [];
            if (Path.Exists(cookedEmotesPath)) customEmoteFolders = Directory.GetDirectories(Path.Combine(cookedEmotesPath));
            string[] customPickaxeFolders = [];
            if (Path.Exists(cookedPickaxesPath)) customPickaxeFolders = Directory.GetDirectories(Path.Combine(cookedPickaxesPath));
            List<string> cosmeticJsons = new();

            Console.WriteLine($"Searching for CIDs inside {cookedAssetsPath}...");
            if (customSkinFolders.Length != 0)
            {
                foreach (string customSkinFolder in customSkinFolders)
                {
                    string[] cid = Directory.GetFiles(customSkinFolder, "*.uasset");
                    if (cid.Length > 0)
                    {
                        string currentFoundCid = Path.GetFileNameWithoutExtension(cid[0]);

                        string json = System.Text.Encoding.UTF8.GetString(TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Cid.json"));
                        var root = JObject.Parse(json);
                        root["ObjectPath"] = root["ObjectPath"]!.Value<string>()!.Replace(
                            $"/Game/Athena/Items/Cosmetics/Characters/CID_Template.CID_Template",
                            $"/Game/Athena/Items/Cosmetics/Characters/{currentFoundCid}.{currentFoundCid}");
                        root["PackageName"] = root["PackageName"]!.Value<string>()!.Replace(
                            $"/Game/Athena/Items/Cosmetics/Characters/CID_Template",
                            $"/Game/Athena/Items/Cosmetics/Characters/{currentFoundCid}");
                        root["AssetName"] = root["AssetName"]!.Value<string>()!.Replace(
                            "CID_Template", currentFoundCid);
                        var tagAndValue = root["TagAndValue"]!.ToArray();
                        foreach (var tag in tagAndValue)
                        {
                            if (tag["Item1"]!.Value<string>() == "PrimaryAssetName")
                            {
                                tag["Item2"] = currentFoundCid;
                                break;
                            }
                        }
                        cosmeticJsons.Add(root.ToString(Formatting.Indented));
                        Console.WriteLine($"Added {currentFoundCid} to the AssetRegistry.bin!");
                    }
                    else
                    {
                        Log.Warning($"no uasset files found inside {customSkinFolder}");
                    }
                }
            }

            Console.WriteLine($"Searching for EIDs inside {cookedAssetsPath}...");
            if (customEmoteFolders.Length != 0)
            {
                foreach (string customEmoteFolder in customEmoteFolders)
                {
                    string[] eid = Directory.GetFiles(customEmoteFolder, "*.uasset");
                    if (eid.Length > 0)
                    {
                        string currentFoundEid = Path.GetFileNameWithoutExtension(eid[0]);

                        string json = System.Text.Encoding.UTF8.GetString(TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Eid.json"));
                        var root = JObject.Parse(json);
                        root["ObjectPath"] = root["ObjectPath"]!.Value<string>()!.Replace(
                            $"/Game/Athena/Items/Cosmetics/Dances/EID_Template.EID_Template",
                            $"/Game/Athena/Items/Cosmetics/Dances/{currentFoundEid}.{currentFoundEid}");
                        root["PackageName"] = root["PackageName"]!.Value<string>()!.Replace(
                            $"/Game/Athena/Items/Cosmetics/Dances/EID_Template",
                            $"/Game/Athena/Items/Cosmetics/Dances/{currentFoundEid}");
                        root["AssetName"] = root["AssetName"]!.Value<string>()!.Replace(
                            "EID_Template", currentFoundEid);
                        var tagAndValue = root["TagAndValue"]!.ToArray();
                        foreach (var tag in tagAndValue)
                        {
                            if (tag["Item1"]!.Value<string>() == "PrimaryAssetName")
                            {
                                tag["Item2"] = currentFoundEid;
                                break;
                            }
                        }
                        cosmeticJsons.Add(root.ToString(Formatting.Indented));
                        Console.WriteLine($"Added {currentFoundEid} to the AssetRegistry.bin!");
                    }
                    else
                    {
                        Log.Warning($"no uasset files found inside {customEmoteFolder}");
                    }
                }
            }

            Console.WriteLine($"Searching for PIDs inside {cookedAssetsPath}...");
            if (customPickaxeFolders.Length != 0)
            {
                foreach (string customPickaxeFolder in customPickaxeFolders)
                {
                    string[] filesFound = Directory.GetFiles(customPickaxeFolder, "*.uasset");
                    string currentFoundPid = string.Empty;
                    if (filesFound.Length > 0) currentFoundPid = Path.GetFileNameWithoutExtension(filesFound[0]);
                    else
                    {
                        Log.Warning($"no uasset files found inside {customPickaxeFolder}");
                        continue;
                    }

                    string json = System.Text.Encoding.UTF8.GetString(TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Pid.json"));
                    var root = JObject.Parse(json);
                    root["ObjectPath"] = root["ObjectPath"]!.Value<string>()!.Replace(
                        $"/Game/Athena/Items/Cosmetics/Pickaxes/Pickaxe_ID_094_Template.Pickaxe_ID_094_Template",
                        $"/Game/Athena/Items/Cosmetics/Pickaxes/{currentFoundPid}.{currentFoundPid}");
                    root["PackageName"] = root["PackageName"]!.Value<string>()!.Replace(
                        $"/Game/Athena/Items/Cosmetics/Pickaxes/Pickaxe_ID_094_Template",
                        $"/Game/Athena/Items/Cosmetics/Pickaxes/{currentFoundPid}");
                    root["AssetName"] = root["AssetName"]!.Value<string>()!.Replace(
                        "Pickaxe_ID_094_Template", currentFoundPid);
                    var tagAndValue = root["TagAndValue"]!.ToArray();
                    foreach (var tag in tagAndValue)
                    {
                        if (tag["Item1"]!.Value<string>() == "PrimaryAssetName")
                        {
                            tag["Item2"] = currentFoundPid;
                            break;
                        }
                    }
                    cosmeticJsons.Add(root.ToString(Formatting.Indented));
                    Console.WriteLine($"Added {currentFoundPid} to the AssetRegistry.bin!");

                    string codename = Path.GetFileName(customPickaxeFolder);
                    string wid = $"WID_Pickaxe_{codename}";

                    json = System.Text.Encoding.UTF8.GetString(TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "Wid.json"));
                    root = JObject.Parse(json);
                    root["ObjectPath"] = root["ObjectPath"]!.Value<string>()!.Replace(
                        $"/Game/Athena/Items/Weapons/WID_Pickaxe_Template.WID_Pickaxe_Template",
                        $"/Game/Athena/Items/Weapons/{wid}.{wid}");
                    root["PackageName"] = root["PackageName"]!.Value<string>()!.Replace(
                        $"/Game/Athena/Items/Weapons/WID_Pickaxe_Template",
                        $"/Game/Athena/Items/Weapons/{wid}");
                    root["AssetName"] = root["AssetName"]!.Value<string>()!.Replace("WID_Pickaxe_Template", wid);
                    tagAndValue = root["TagAndValue"]!.ToArray();
                    foreach (var tag in tagAndValue)
                    {
                        if (tag["Item1"]!.Value<string>() == "PrimaryAssetName")
                        {
                            tag["Item2"] = wid;
                            continue;
                        }
                        else if (tag["Item1"].Value<string>() == "AssetBundleData")
                        {
                            tag["Item2"] = 
                            "(Bundles=((BundleName=\"Equipped\"," +
                            $"BundleAssets=(/Game/Weapons/FORT_Melee/Blueprints/Impact/B_Melee_Impact_Pickaxe_Athena_{codename}.B_Melee_Impact_Pickaxe_Athena_{codename}_C," +
                            "/Game/Items/Alterations/DamageTypeAlterations/BaseAlteration_ImpactDamage.BaseAlteration_ImpactDamage," +
                            "/Game/Athena/Items/Weapons/Abilities/GAB_Melee_ImpactCombo_Athena.GAB_Melee_ImpactCombo_Athena_C))," +
                            "(BundleName=\"ClientAthenaPreload\"," +
                            $"BundleAssets=(/Game/Weapons/FORT_Melee/Blueprints/Impact/B_Melee_Impact_Pickaxe_Athena_{codename}.B_Melee_Impact_Pickaxe_Athena_{codename}_C," +
                            "/Game/Athena/Items/Weapons/Abilities/GAB_Melee_ImpactCombo_Athena.GAB_Melee_ImpactCombo_Athena_C))," +
                            "(BundleName=\"ServerAthenaPreload\"," +
                            $"BundleAssets=(/Game/Weapons/FORT_Melee/Blueprints/Impact/B_Melee_Impact_Pickaxe_Athena_{codename}.B_Melee_Impact_Pickaxe_Athena_{codename}_C))," +
                            "(BundleName=\"ItemsInInventoryInZone\"," +
                            "BundleAssets=(/Game/Athena/Items/Weapons/Abilities/GAB_Melee_ImpactCombo_Athena.GAB_Melee_ImpactCombo_Athena_C))," +
                            "(BundleName=\"DroppedItem\"," +
                            "BundleAssets=(/Game/Weapons/FORT_Melee/Meshes/SK_Pick.SK_Pick," +
                            "/Game/Sounds/Fort_GamePlay_Sounds/Loot/Collect_Loot.Collect_Loot))," +
                            "(BundleName=\"ServerAthena\"," +
                            $"BundleAssets=(/Game/Weapons/FORT_Melee/Blueprints/Impact/B_Melee_Impact_Pickaxe_Athena_{codename}.B_Melee_Impact_Pickaxe_Athena_{codename}_C))))";
                        }
                    }
                    cosmeticJsons.Add(root.ToString(Formatting.Indented));
                    Console.WriteLine($"Added {wid} to the AssetRegistry.bin!");
                }
            }

            DeleteOldFnGamePath(currentCosmeticPath);

            if (Path.Exists(outputFnGamePath)) Directory.Delete(outputFnGamePath, true);
            Directory.CreateDirectory(outputFnGamePath);

            AssetRegistryHelper.Inject(TemplateLoader.GetEmbeddedFile(ueVersionNumber, "RawUeAssets", "AssetRegistry.bin"), cosmeticJsons.ToArray(), 
            Path.Combine(outputFnGamePath, "AssetRegistry312398E80AB6209B22CAA2EBAB2DB35B.bin"));
        }

        private static void DeleteOldFnGamePath(string emotePath)
        {
            string oldOutputPath = Path.Combine(emotePath, "Output", "FortniteGame");
            if (Directory.Exists(oldOutputPath)) Directory.Delete(oldOutputPath, true); //For OG users ;)
        }
    }
}
