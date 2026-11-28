using Microsoft.UI.Xaml;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UFMT.Core;
using UFMT.UI;
using Windows.Foundation;
using WinRT.Interop;

namespace UFMT.FnAssets
{
    internal static class PickaxeAssetCreator
    {
        public static Dictionary<string, string> SeriesCodenames = new(){ {"Dark Series", "CUBESeries"}, { "Star Wars Series", "ColumbusSeries" },
        {"Icon Series", "CreatorCollabSeries"}, {"DC Series", "DCUSeries"}, {"Frozen Series", "FrozenSeries" }, {"Lava Series", "LavaSeries"},
        {"Marvel Series", "MarvelSeries"}, {"Shadow Series", "ShadowSeries"},  {"Slurp Series", "SlurpSeries"},
        {"Test Series", "FakeToken_FDS_Series"}, {"Anual Pass Series", "2020AnnualPassSeries"}};

        internal static void CopyFilesFromUe(string contentFolderPath, DirectoryInfo cookedPickaxeDirectory)
        {
            if (!Path.Exists(contentFolderPath)) Directory.CreateDirectory(contentFolderPath);
            foreach (DirectoryInfo subFolder in cookedPickaxeDirectory.GetDirectories("*", SearchOption.AllDirectories))
            {
                string targetSubDir = subFolder.FullName.Replace(cookedPickaxeDirectory.FullName, contentFolderPath);
                Directory.CreateDirectory(targetSubDir);

                foreach (FileInfo file in subFolder.GetFiles())
                {
                    file.CopyTo(Path.Combine(targetSubDir, file.Name), true);
                }
            }

            Log.Success($"Copied files from {cookedPickaxeDirectory} to {contentFolderPath}");
        }

        internal static void CreateMaterials(string contentFolderPath, string codename, ObservableCollection<PickaxeMaterial> materials, 
        FnVersion fnVersion, EngineVersion uassetApiEngineVersion, string uePickaxesPackagePath)
        {
            string materialsPath = Path.Combine(contentFolderPath, "Materials");
            foreach (var material in materials)
            {
                string uassetMaterialPath = Path.Combine(materialsPath, $"{material.Name}.uasset");
                string uexpMaterialPath = Path.Combine(materialsPath, $"{material.Name}.uexp");

                byte[] materialUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "MiNoSwizzle.uasset");
                byte[] materialUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "MiNoSwizzle.uexp");
                if (!fnVersion.ManuallySwizzleMaterials && material.Swizzle)
                {
                    materialUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "MiSwizzle.uasset");
                    materialUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "MiSwizzle.uexp");
                }

                File.WriteAllBytes(Path.Combine(uassetMaterialPath), materialUassetBase64);
                File.WriteAllBytes(Path.Combine(uexpMaterialPath), materialUexpBase64);
                Log.Success($"Created material instance {material.Name}");
                Console.WriteLine($"Editing {material.Name}");

                var currentMi = new UAsset(uassetMaterialPath, uassetApiEngineVersion);
                var miImportData = currentMi.Imports;
                var miExportData = currentMi.Exports;
                var miExport0 = (NormalExport)currentMi.Exports[0];
                string fnTexturesPath = $"{uePickaxesPackagePath}/{codename}/Textures/";
                miImportData[fnVersion.DiffusePathIndex].ObjectName.Value.Value = Path.Combine(fnTexturesPath, material.SelectedDiffuse);
                Console.WriteLine($"Changed the diffuse texture path in {material.Name} to {Path.Combine(fnTexturesPath, material.SelectedDiffuse)}");
                miImportData[fnVersion.DiffusePathIndex + 1].ObjectName.Value.Value = Path.Combine(fnTexturesPath, material.SelectedMask);
                Console.WriteLine($"Changed the mask texture path in {material.Name} to {Path.Combine(fnTexturesPath, material.SelectedMask)}");
                miImportData[fnVersion.DiffusePathIndex + 2].ObjectName.Value.Value = Path.Combine(fnTexturesPath, material.SelectedNormal);
                Console.WriteLine($"Changed the normal texture path in {material.Name} to {Path.Combine(fnTexturesPath, material.SelectedNormal)}");
                miImportData[fnVersion.DiffusePathIndex + 3].ObjectName.Value.Value = Path.Combine(fnTexturesPath, material.SelectedSpecular);
                Console.WriteLine($"Changed the specular texture path in {material.Name} to {Path.Combine(fnTexturesPath, material.SelectedSpecular)}");
                miImportData[fnVersion.DiffuseNameIndex].ObjectName.Value.Value = material.SelectedDiffuse;
                Console.WriteLine($"Changed the diffuse texture in {material.Name} to {material.SelectedDiffuse}");
                miImportData[fnVersion.DiffuseNameIndex + 1].ObjectName.Value.Value = material.SelectedMask;
                Console.WriteLine($"Changed the mask texture in {material.Name} to {material.SelectedMask}");
                miImportData[fnVersion.DiffuseNameIndex + 2].ObjectName.Value.Value = material.SelectedNormal;
                Console.WriteLine($"Changed the normal texture in {material.Name} to {material.SelectedNormal}");
                miImportData[fnVersion.DiffuseNameIndex + 3].ObjectName.Value.Value = material.SelectedSpecular;
                Console.WriteLine($"Changed the specular texture in {material.Name} to {material.SelectedSpecular}");
                miExportData[0].ObjectName.Value.Value = material.Name;

                currentMi.Write(uassetMaterialPath);
                Log.Success($"Successfully edited {material.Name}.uasset and {material.Name}.uexp");
            }
        }

        internal static void CreateImpactSoundCue(string contentFolderPath, string codename, string[] impactSoundWaves,
            FnVersion fnVersion, EngineVersion uassetApiEngineVersion, string uePickaxesPackagePath)
        {
            string soundsPath = Path.Combine(contentFolderPath, "Sounds");
            string scName = $"SC_Pickaxe_Impact_{codename}";
            string scUassetPath = Path.Combine(soundsPath, $"{scName}.uasset");
            string scUexpPath = Path.ChangeExtension(scUassetPath, ".uexp");

            byte[] scUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "PickaxeSoundCueImpact.uasset");
            byte[] scUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "PickaxeSoundCueImpact.uexp");

            File.WriteAllBytes(scUassetPath, scUassetBase64);
            File.WriteAllBytes(scUexpPath, scUexpBase64);
            Log.Success($"Created sound cue {scName}");
            Console.WriteLine($"Editing {scName}");

            var scAsset = new UAsset(scUassetPath, uassetApiEngineVersion);
            var scExportData = scAsset.Exports;
            int exportIndex = 2;

            scAsset.AddNameReference(FString.FromString(scName));
            scExportData[0].ObjectName = FName.FromString(scAsset, scName);

            foreach (string impactSoundFileName in impactSoundWaves)
            {
                if (exportIndex >= scExportData.Count)
                {
                    var targetExport = (NormalExport)scExportData[2];
                    var clonedExport = (NormalExport)targetExport.Clone();

                    string newExportName = $"SoundNodeWavePlayer_{exportIndex - 2}";
                    scAsset.AddNameReference(FString.FromString(newExportName));
                    clonedExport.ObjectName = FName.FromString(scAsset, newExportName);

                    var clonedPtr = (SoftObjectPropertyData)clonedExport.Data[0].Clone();
                    clonedExport.Data = new List<PropertyData> { clonedPtr };

                    scExportData.Add(clonedExport);
                }

                var currentSoundWaveExport = (NormalExport)scExportData[exportIndex];
                var swAssetPtr = (SoftObjectPropertyData)currentSoundWaveExport[0];
                string impactSoundAssetName = Path.GetFileNameWithoutExtension(impactSoundFileName);
                string fullAssetPath = $"{uePickaxesPackagePath}/{codename}/Sounds/{impactSoundAssetName}.{impactSoundAssetName}";
                scAsset.AddNameReference(FString.FromString(fullAssetPath));

                var path = swAssetPtr.Value;
                path.AssetPath.AssetName = FName.FromString(scAsset, fullAssetPath);
                swAssetPtr.Value = path;
                exportIndex++;
            }

            while (scExportData.Count > exportIndex) scExportData.RemoveAt(exportIndex);

            var randomNodeExport = (NormalExport)scExportData[1];
            var weightsArray = (ArrayPropertyData)randomNodeExport["Weights"];
            var childNodesArray = (ArrayPropertyData)randomNodeExport["ChildNodes"];

            int waveCount = impactSoundWaves.Length;
            var newWeights = new PropertyData[waveCount];
            var newChildNodes = new PropertyData[waveCount];
            var newDeps = new List<FPackageIndex>(waveCount);

            for (int i = 0; i < waveCount; i++)
            {
                scAsset.AddNameReference(FString.FromString(i.ToString()));
                FName elementName = FName.FromString(scAsset, i.ToString());

                newWeights[i] = new FloatPropertyData(elementName)
                {
                    Value = 1.0f
                };

                var pkgIdx = FPackageIndex.FromExport(i + 2);
                newChildNodes[i] = new ObjectPropertyData(elementName)
                {
                    Value = pkgIdx
                };
                newDeps.Add(pkgIdx);
            }

            weightsArray.Value = newWeights;
            childNodesArray.Value = newChildNodes;

            randomNodeExport.CreateBeforeSerializationDependencies = new List<FPackageIndex>(newDeps);
            randomNodeExport.SerializationBeforeCreateDependencies = new List<FPackageIndex>(newDeps);

            scAsset.Write(scUassetPath);
            Log.Success($"Successfully edited {scName}.uasset and {scName}.uexp");
        }

        internal static void CreateReadyAndSwingSoundCues(string contentFolderPath, string codename, string[] readySoundWaves, string[] swingSoundWaves,
            FnVersion fnVersion, EngineVersion uassetApiEngineVersion, string uePickaxesPackagePath)
        {
            string currentSoundCueType = "Swing";
            for (int i1 = 0; i1 < 2; i1++)
            {
                string[] currentScTypeSoundWaves = currentSoundCueType == "Swing" ? swingSoundWaves : readySoundWaves;
                string soundsPath = Path.Combine(contentFolderPath, "Sounds");
                string scName = $"SC_Pickaxe_{currentSoundCueType}_{codename}";
                string scUassetPath = Path.Combine(soundsPath, $"{scName}.uasset");
                string scUexpPath = Path.ChangeExtension(scUassetPath, ".uexp");

                byte[] scUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "PickaxeSoundCueReadyOrSwing.uasset");
                byte[] scUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "PickaxeSoundCueReadyOrSwing.uexp");

                File.WriteAllBytes(scUassetPath, scUassetBase64);
                File.WriteAllBytes(scUexpPath, scUexpBase64);
                Log.Success($"Created sound cue {scName}");
                Console.WriteLine($"Editing {scName}");

                var scAsset = new UAsset(scUassetPath, uassetApiEngineVersion);
                var scExportData = scAsset.Exports;

                scAsset.AddNameReference(FString.FromString(scName));
                scExportData[0].ObjectName = FName.FromString(scAsset, scName);

                int[] existingSwingIndices = { 12, 25, 27, 17, 18, 19 };
                var swingExportIndices = new List<int>();
                int nextPlayerNum = 26;

                for (int i = 0; i < currentScTypeSoundWaves.Length; i++)
                {
                    int targetIdx;
                    if (i < existingSwingIndices.Length)
                    {
                        targetIdx = existingSwingIndices[i];
                    }
                    else
                    {
                        var targetExport = (NormalExport)scExportData[12];
                        var clonedExport = (NormalExport)targetExport.Clone();

                        string newExportName = $"SoundNodeWavePlayer_{nextPlayerNum++}";
                        scAsset.AddNameReference(FString.FromString(newExportName));
                        clonedExport.ObjectName = FName.FromString(scAsset, newExportName);

                        var clonedPtr = (SoftObjectPropertyData)clonedExport.Data[0].Clone();
                        clonedExport.Data = new List<PropertyData> { clonedPtr };

                        scExportData.Add(clonedExport);
                        targetIdx = scExportData.Count - 1;
                    }

                    var currentSoundWaveExport = (NormalExport)scExportData[targetIdx];
                    var swAssetPtr = (SoftObjectPropertyData)currentSoundWaveExport[0];
                    string assetName = Path.GetFileNameWithoutExtension(currentScTypeSoundWaves[i]);
                    string fullAssetPath = $"{uePickaxesPackagePath}/{codename}/Sounds/{assetName}.{assetName}";
                    scAsset.AddNameReference(FString.FromString(fullAssetPath));

                    var path = swAssetPtr.Value;
                    path.AssetPath.AssetName = FName.FromString(scAsset, fullAssetPath);
                    swAssetPtr.Value = path;

                    swingExportIndices.Add(targetIdx);
                }

                var usedSwingSet = new HashSet<int>(swingExportIndices);
                var exportsToRemove = new HashSet<int>(existingSwingIndices.Where(idx => !usedSwingSet.Contains(idx)));
                exportsToRemove.UnionWith(new[] { 21, 22, 23 });

                var indexMap = new Dictionary<int, int>();
                int newIdx = 0;
                for (int oldIdx = 0; oldIdx < scExportData.Count; oldIdx++)
                {
                    if (!exportsToRemove.Contains(oldIdx))
                        indexMap[oldIdx] = newIdx++;
                }

                for (int oldIdx = scExportData.Count - 1; oldIdx >= 0; oldIdx--)
                {
                    if (exportsToRemove.Contains(oldIdx))
                        scExportData.RemoveAt(oldIdx);
                }

                foreach (var exp in scExportData)
                {
                    RemapDependencyList(exp.CreateBeforeSerializationDependencies, indexMap);
                    RemapDependencyList(exp.SerializationBeforeCreateDependencies, indexMap);
                    RemapDependencyList(exp.CreateBeforeCreateDependencies, indexMap);
                    RemapDependencyList(exp.SerializationBeforeSerializationDependencies, indexMap);
                }

                RemapRandomNodeIndices((NormalExport)scExportData[8], indexMap);
                RemapRandomNodeIndices((NormalExport)scExportData[9], indexMap);

                var remappedSwingIndices = swingExportIndices.Select(oldIdx => indexMap[oldIdx]).ToList();
                UpdateRandomNode(scAsset, (NormalExport)scExportData[10], remappedSwingIndices);
                UpdateRandomNode(scAsset, (NormalExport)scExportData[11], remappedSwingIndices);

                scAsset.Write(scUassetPath);
                Log.Success($"Successfully edited {scName}.uasset and {scName}.uexp");

                currentSoundCueType = "Ready";
                if (readySoundWaves.Length == 0)
                {
                    scName = $"SC_Pickaxe_{currentSoundCueType}_{codename}";
                    Log.Success($"Created sound cue {scName}");
                    Console.WriteLine($"Editing {scName}");
                    string readyScUassetPath = Path.Combine(soundsPath, $"{scName}.uasset");
                    File.Copy(scUassetPath, readyScUassetPath, true);
                    File.Copy(scUexpPath, Path.ChangeExtension(readyScUassetPath, ".uexp"), true);
                    scAsset = new UAsset(readyScUassetPath, uassetApiEngineVersion);
                    scExportData = scAsset.Exports;
                    scAsset.AddNameReference(FString.FromString(scName));
                    scExportData[0].ObjectName = FName.FromString(scAsset, scName);
                    scAsset.Write(readyScUassetPath);
                    Log.Success($"Successfully edited {scName}.uasset and {scName}.uexp");
                    return;
                }
            }

            void UpdateRandomNode(UAsset asset, NormalExport randomNodeExport, List<int> exportIndices)
            {
                var weightsArray = (ArrayPropertyData)randomNodeExport["Weights"];
                var childNodesArray = (ArrayPropertyData)randomNodeExport["ChildNodes"];

                var newWeights = new PropertyData[exportIndices.Count];
                var newChildNodes = new PropertyData[exportIndices.Count];
                var newDeps = new List<FPackageIndex>(exportIndices.Count);

                for (int i = 0; i < exportIndices.Count; i++)
                {
                    asset.AddNameReference(FString.FromString(i.ToString()));
                    FName elementName = FName.FromString(asset, i.ToString());

                    newWeights[i] = new FloatPropertyData(elementName)
                    {
                        Value = 1.0f
                    };

                    var pkgIdx = FPackageIndex.FromExport(exportIndices[i]);
                    newChildNodes[i] = new ObjectPropertyData(elementName)
                    {
                        Value = pkgIdx
                    };
                    newDeps.Add(pkgIdx);
                }

                weightsArray.Value = newWeights;
                childNodesArray.Value = newChildNodes;

                randomNodeExport.CreateBeforeSerializationDependencies = new List<FPackageIndex>(newDeps);
                randomNodeExport.SerializationBeforeCreateDependencies = new List<FPackageIndex>(newDeps);
            }

            void RemapRandomNodeIndices(NormalExport randomNodeExport, Dictionary<int, int> indexMap)
            {
                var childNodesArray = (ArrayPropertyData)randomNodeExport["ChildNodes"];
                foreach (ObjectPropertyData child in childNodesArray.Value)
                {
                    int oldZeroBased = child.Value.Index - 1;
                    if (indexMap.TryGetValue(oldZeroBased, out int newZeroBased))
                        child.Value = FPackageIndex.FromExport(newZeroBased);
                }
            }

            void RemapDependencyList(List<FPackageIndex> deps, Dictionary<int, int> indexMap)
            {
                if (deps == null) return;
                for (int i = deps.Count - 1; i >= 0; i--)
                {
                    if (deps[i].Index > 0)
                    {
                        int oldZeroBased = deps[i].Index - 1;
                        if (indexMap.TryGetValue(oldZeroBased, out int newZeroBased))
                            deps[i] = FPackageIndex.FromExport(newZeroBased);
                        else
                            deps.RemoveAt(i);
                    }
                }
            }
        }

        internal static void CreateBMeleeImpact(string outputFnGamePath, string codename, FnVersion fnVersion, EngineVersion uassetApiEngineVersion, 
        string uePickaxesPackagePath)
        {
            string meleeImpactPath = Path.Combine(outputFnGamePath, "Content", "Weapons", "FORT_Melee", "Blueprints", "Impact");
            Directory.CreateDirectory(meleeImpactPath);
            string meleeImpactName = $"B_Melee_Impact_Pickaxe_Athena_{codename}";
            string meleeImpactUassetPath = Path.Combine(meleeImpactPath, $"{meleeImpactName}.uasset");
            string meleeImpactUexpPath = Path.ChangeExtension(meleeImpactUassetPath, ".uexp");

            byte[] meleeImpactUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "BMeleeImpact.uasset");
            byte[] meleeImpactUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "BMeleeImpact.uexp");

            File.WriteAllBytes(Path.Combine(meleeImpactUassetPath), meleeImpactUassetBase64);
            File.WriteAllBytes(Path.Combine(meleeImpactUexpPath), meleeImpactUexpBase64);
            Log.Success($"Created weapon actor class {meleeImpactName}");
            Console.WriteLine($"Editing {meleeImpactName}");

            var asset = new UAsset(meleeImpactUassetPath, uassetApiEngineVersion);
            var exportData = asset.Exports;
            exportData[0].ObjectName.Value.Value = $"B_Melee_Impact_Pickaxe_Athena_{codename}_C";
            exportData[1].ObjectName.Value.Value = $"Default__B_Melee_Impact_Pickaxe_Athena_{codename}_C";

            var importData = asset.Imports;

            importData[15].ObjectName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Sounds/SC_Pickaxe_Impact_{codename}";
            importData[16].ObjectName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Sounds/SC_Pickaxe_Ready_{codename}";
            importData[17].ObjectName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Sounds/SC_Pickaxe_Swing_{codename}";

            importData[34].ObjectName.Value.Value = $"SC_Pickaxe_Impact_{codename}";
            importData[35].ObjectName.Value.Value = $"SC_Pickaxe_Ready_{codename}";
            importData[36].ObjectName.Value.Value = $"SC_Pickaxe_Swing_{codename}";

            asset.Write(meleeImpactUassetPath);
            Log.Success($"Successfully edited {meleeImpactName}.uasset and {meleeImpactName}.uexp");
        }

        internal static void CreateWid(string outputFnGamePath, string codename, FnVersion fnVersion, EngineVersion uassetApiEngineVersion,
        string uePickaxesPackagePath, string smallIcon, string largeIcon, string rarity, string series)
        {
            try
            {
                string weaponsPath = Path.Combine(outputFnGamePath, "Content", "Athena", "Items", "Weapons");
                Directory.CreateDirectory(weaponsPath);
                string assetName = $"WID_Pickaxe_{codename}";
                string widUassetPath = Path.Combine(weaponsPath, $"{assetName}.uasset");
                string widUexpPath = Path.ChangeExtension(widUassetPath, ".uexp");

                byte[] widUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Wid.uasset");
                byte[] widUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Wid.uexp");

                File.WriteAllBytes(Path.Combine(widUassetPath), widUassetBase64);
                File.WriteAllBytes(Path.Combine(widUexpPath), widUexpBase64);
                Log.Success($"Created {assetName}");
                Console.WriteLine($"Editing {assetName}");

                var asset = new UAsset(widUassetPath, uassetApiEngineVersion);
                var exportData = asset.Exports;
                var importData = asset.Imports;
                exportData[0].ObjectName.Value.Value = assetName;
                var export1 = (NormalExport)exportData[0];

                var weaponActorClass = (SoftObjectPropertyData)export1["WeaponActorClass"];
                weaponActorClass.Value.AssetPath.AssetName.Value.Value =
                $"/Game/Weapons/FORT_Melee/Blueprints/Impact/B_Melee_Impact_Pickaxe_Athena_{codename}.B_Melee_Impact_Pickaxe_Athena_{codename}_C";

                var weaponMeshOverride = (SoftObjectPropertyData)export1["WeaponMeshOverride"];
                weaponMeshOverride.Value.AssetPath.AssetName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Meshes/SK_{codename}.SK_{codename}";

                var smallIconExport = (SoftObjectPropertyData)export1["SmallPreviewImage"];
                var largeIconExport = (SoftObjectPropertyData)export1["LargePreviewImage"];
                smallIconExport.Value.AssetPath.AssetName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Textures/{smallIcon}.{smallIcon}";
                largeIconExport.Value.AssetPath.AssetName.Value.Value = $"{uePickaxesPackagePath}/{codename}/Textures/{largeIcon}.{largeIcon}";

                if (series == "None") export1.Data.RemoveAt(33);
                else
                {
                    string seriesCodename = SeriesCodenames.GetValueOrDefault(series) ?? series;
                    importData[3].ObjectName.Value.Value = seriesCodename;
                    importData[5].ObjectName.Value.Value = $"/Game/Athena/Items/Cosmetics/Series/{seriesCodename}";
                    Console.WriteLine($"Changed the Series in {assetName} to {series}");
                }


                var rarityExport = (EnumPropertyData)export1["Rarity"];
                rarityExport.Value.Value.Value = $"EFortRarity::{rarity}";

                if (rarity == "Uncommon") export1.Data.RemoveAt(23); //Removes the rarity property since no rarity is equal to uncommon in fn
                else if (rarity == "Unattainable (Impossible T7)") rarityExport.Value.Value.Value = $"EFortRarity::Unattainable";
                if ((fnVersion.Name == "8.51-9.10" || fnVersion.Name == "9.41") && rarity != "Uncommon")
                {
                    string rarityCodename = "";
                    if (rarity == "Common") rarityCodename = "Handmade";
                    else if (rarity == "Rare") rarityCodename = "Sturdy";
                    else if (rarity == "Epic") rarityCodename = "Quality";
                    else if (rarity == "Legendary") rarityCodename = "Fine";
                    else if (rarity == "Mythic") rarityCodename = "Elegant";
                    else if (rarity == "Transcendent") rarityCodename = "Masterwork";
                    else if (rarity == "Unattainable (Impossible T7)") rarityCodename = "Epic";
                    rarityExport.Value.Value.Value = $"EFortRarity::{rarityCodename}";
                }

                Console.WriteLine($"Changed the Rarity in {assetName} to {rarity}");

                asset.Write(widUassetPath);
                Log.Success($"Successfully edited {assetName}.uasset and {assetName}.uexp");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while editing WID_Pickaxe_{codename}.uasset {ex}");
            }
        }

        internal static void CreatePid(string outputFnGamePath, string pid, string codename, string name, string description, 
        string rarity, string series, FnVersion fnVersion, EngineVersion uassetApiEngineVersion)
        {
            try
            {
                Console.WriteLine($"Editing {pid}.uasset");
                string pidPath = Path.Combine(outputFnGamePath, "Content", "Athena", "Items",
                "Cosmetics", "Pickaxes");
                if (!Path.Exists(pidPath)) Directory.CreateDirectory(pidPath);
                string pidUassetPath = Path.Combine(pidPath, $"{pid}.uasset");
                string pidUexpPath = Path.Combine(pidPath, $"{pid}.uexp");
                File.WriteAllBytes(pidUassetPath, TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Pid.uasset"));
                File.WriteAllBytes(pidUexpPath, TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Pid.uexp"));

                var currentPid = new UAsset(pidUassetPath, uassetApiEngineVersion);
                var pidExport0 = (NormalExport)currentPid.Exports[0];
                var pidImport = currentPid.Imports;
                pidImport[3].ObjectName.Value.Value = $"WID_Pickaxe_{codename}";
                Console.WriteLine($"Changed the WID in {pid} to WID_Pickaxe_{codename}");
                pidImport[5].ObjectName.Value.Value = $"/Game/Athena/Items/Weapons/WID_Pickaxe_{codename}";
                Console.WriteLine($"Changed the WID path in {pid} to /Game/Athena/Items/Weapons/WID_Pickaxe_{codename}");

                pidExport0.ObjectName.Value.Value = pid;

                if (series == "None") pidExport0.Data.RemoveAt(9);
                else
                {
                    string seriesCodename = SeriesCodenames.GetValueOrDefault(series) ?? series;
                    pidImport[3].ObjectName.Value.Value = seriesCodename;
                    pidImport[5].ObjectName.Value.Value = $"/Game/Athena/Items/Cosmetics/Series/{seriesCodename}";
                    Console.WriteLine($"Changed the Series in {pid} to {series}");
                }

                pidExport0.Data.RemoveAt(7); //Removes gameplay tags

                var rarityExport = (EnumPropertyData)pidExport0["Rarity"];
                rarityExport.Value.Value.Value = $"EFortRarity::{rarity}";

                if (rarity == "Uncommon") pidExport0.Data.RemoveAt(3); //Removes the rarityExport property since no rarityExport is equal to uncommon in fn
                else if (rarity == "Unattainable (Impossible T7)") rarityExport.Value.Value.Value = $"EFortRarity::Unattainable";
                if ((fnVersion.Name == "8.51-9.10" || fnVersion.Name == "9.41") && rarity != "Uncommon")
                {
                    string rarityExportCodename = "";
                    if (rarity == "Common") rarityExportCodename = "Handmade";
                    else if (rarity == "Rare") rarityExportCodename = "Sturdy";
                    else if (rarity == "Epic") rarityExportCodename = "Quality";
                    else if (rarity == "Legendary") rarityExportCodename = "Fine";
                    else if (rarity == "Mythic") rarityExportCodename = "Elegant";
                    else if (rarity == "Transcendent") rarityExportCodename = "Masterwork";
                    else if (rarity == "Unattainable (Impossible T7)") rarityExportCodename = "Epic";
                    rarityExport.Value.Value.Value = $"EFortRarity::{rarityExportCodename}";
                }

                Console.WriteLine($"Changed the Rarity in {pid} to {rarity}");
                ((TextPropertyData)pidExport0["DisplayName"]).CultureInvariantString.Value = name;
                Console.WriteLine($"Changed the DisplayName in {pid} to {name}");
                ((TextPropertyData)pidExport0["Description"]).CultureInvariantString.Value = description;
                Console.WriteLine($"Changed the Description in {pid} to {description}");
                string displayNameKey = Guid.NewGuid().ToString("N").ToUpper(); //Generates a new key for the display name since multiple display names can't use the same key
                string descriptionKey = Guid.NewGuid().ToString("N").ToUpper();
                ((TextPropertyData)pidExport0["DisplayName"]).Value.Value = displayNameKey;
                ((TextPropertyData)pidExport0["Description"]).Value.Value = descriptionKey;

                currentPid.Write(pidUassetPath);
                Log.Success($"Successfully edited {pid}.uasset");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while editing {pid}.uasset {ex}");
            }
        }
    }
}
