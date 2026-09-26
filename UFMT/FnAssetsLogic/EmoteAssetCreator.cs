using Microsoft.UI.Xaml;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UAssetAPI;
using UAssetAPI.CustomVersions;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UFMT.Core;
using UFMT.UI;
using UFMT.UnrealEngine;
using WinRT.Interop;

namespace UFMT.FnAssets
{
    internal static class EmoteAssetCreator
    {
        internal static void CopyFilesFromUe(string contentFolderPath, DirectoryInfo cookedEmoteDirectory)
        {
            if (!Path.Exists(contentFolderPath)) Directory.CreateDirectory(contentFolderPath);
            foreach (DirectoryInfo subFolder in cookedEmoteDirectory.GetDirectories("*", SearchOption.AllDirectories))
            {
                string targetSubDir = subFolder.FullName.Replace(cookedEmoteDirectory.FullName, contentFolderPath);
                Directory.CreateDirectory(targetSubDir);

                foreach (FileInfo file in subFolder.GetFiles())
                {
                    file.CopyTo(Path.Combine(targetSubDir, file.Name), true);
                }
            }
            Log.Success($"Copied files from {cookedEmoteDirectory} to {contentFolderPath}");

        }

        internal static void CreateAnimationMontage(string OutputFnGameCurrentEmotePath, string animationName, float animationLength,
        FnVersion fnVersion, UeVersion ueVersion, string ueEmotesPackagePath, string codename, string animationJson, float loopSectionStart)
        {
            try
            {
                string montageName = $"{animationName}_M.uasset";
                string animationMontageUassetPath = Path.Combine(OutputFnGameCurrentEmotePath, "Animations", montageName);

                byte[] montageUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteMontage.uasset");
                byte[] montageUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteMontage.uexp");

                File.WriteAllBytes(animationMontageUassetPath, montageUassetBase64);
                File.WriteAllBytes(Path.ChangeExtension(animationMontageUassetPath, ".uexp"), montageUexpBase64);

                var animationMontageAsset = new UAsset(animationMontageUassetPath, ueVersion.UassetApiEngineVer);
                var exportData = animationMontageAsset.Exports;
                var export0 = (NormalExport)exportData[0];

                export0.ObjectName.Value.Value = Path.GetFileNameWithoutExtension(animationMontageUassetPath);

                var sequenceLength = (FloatPropertyData)export0["SequenceLength"];
                sequenceLength.Value = animationLength;

                var compositeSections = (ArrayPropertyData)export0["CompositeSections"];
                var defaultCompositeSection = (StructPropertyData)compositeSections.Value[0];
                var loopCompositeSection = (StructPropertyData)compositeSections.Value[1];
                var defaultCompositeSectionSegmentLength = (FloatPropertyData)defaultCompositeSection["SegmentLength"];
                var loopCompositeSectionSegmentLength = (FloatPropertyData)loopCompositeSection["SegmentLength"];
                var loopCompositeSectionLinkValue = (FloatPropertyData)loopCompositeSection["LinkValue"];
                defaultCompositeSectionSegmentLength.Value = animationLength;
                loopCompositeSectionSegmentLength.Value = animationLength;
                loopCompositeSectionLinkValue.Value = loopSectionStart;

                var slotAnimTracks = (ArrayPropertyData)export0["SlotAnimTracks"];
                var slotAnimTracks2 = (StructPropertyData)slotAnimTracks.Value[0];
                var animTrack = (StructPropertyData)slotAnimTracks2.Value[1];
                var animSegments = (ArrayPropertyData)animTrack.Value[0];
                var animSegments2 = (StructPropertyData)animSegments.Value[0];
                var animEndTime = (FloatPropertyData)animSegments2["AnimEndTime"];
                animEndTime.Value = animationLength;

                var notifies = (ArrayPropertyData)export0["Notifies"];

                var emoteSoundNotify = (StructPropertyData)notifies.Value[0];
                var emoteSoundNotifyDuration = (FloatPropertyData)emoteSoundNotify["Duration"];
                var emoteSoundNotifySegmentLength = (FloatPropertyData)emoteSoundNotify["SegmentLength"];
                emoteSoundNotifyDuration.Value = animationLength;
                emoteSoundNotifySegmentLength.Value = animationLength;
                var emoteSoundNotifyEndLink = (StructPropertyData)emoteSoundNotify["EndLink"];
                var emoteSoundNotifyEndLinkSegmentLength = (FloatPropertyData)emoteSoundNotifyEndLink["SegmentLength"];
                var emoteSoundNotifyEndLinkLinkValue = (FloatPropertyData)emoteSoundNotifyEndLink["LinkValue"];
                emoteSoundNotifyEndLinkSegmentLength.Value = animationLength;
                emoteSoundNotifyEndLinkLinkValue.Value = animationLength;

                var holsterWeaponNotify = (StructPropertyData)notifies.Value[1];
                var holsterWeaponNotifyDuration = (FloatPropertyData)holsterWeaponNotify["Duration"];
                var holsterWeaponNotifySegmentLength = (FloatPropertyData)holsterWeaponNotify["SegmentLength"];
                holsterWeaponNotifyDuration.Value = animationLength;
                holsterWeaponNotifySegmentLength.Value = animationLength;
                var holsterWeaponNotifyEndLink = (StructPropertyData)holsterWeaponNotify["EndLink"];
                var holsterWeaponNotifyEndLinkSegmentLength = (FloatPropertyData)holsterWeaponNotifyEndLink["SegmentLength"];
                var holsterWeaponNotifyEndLinkLinkValue = (FloatPropertyData)holsterWeaponNotifyEndLink["LinkValue"];
                holsterWeaponNotifyEndLinkSegmentLength.Value = animationLength;
                holsterWeaponNotifyEndLinkLinkValue.Value = animationLength;

                var rawCurveData = (StructPropertyData)export0["RawCurveData"];
                var floatCurves = (ArrayPropertyData)rawCurveData.Value[0];
                if (animationJson == string.Empty)
                {
                    // Remove DisableFaceOverride if no .json is provided since there is no way to get the animation's facial animations
                    var curveList = floatCurves.Value.ToList();
                    curveList.RemoveAt(2);
                    floatCurves.Value = curveList.ToArray();
                }

                var importData = animationMontageAsset.Imports;
                importData[2].ObjectName.Value.Value = animationName;
                importData[9].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Animations/{animationName}";
                importData[10].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/SC_EmoteMusic3P_{codename}";
                importData[11].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/SC_EmoteMusic_{codename}";
                importData[15].ObjectName.Value.Value = $"SC_EmoteMusic3P_{codename}";
                importData[16].ObjectName.Value.Value = $"SC_EmoteMusic_{codename}";


                animationMontageAsset.Write(animationMontageUassetPath);
                Log.Success($"Succesfully edited {Path.GetFileNameWithoutExtension(animationMontageUassetPath)}");
            }

            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        internal static void CreateSoundCues(string OutputFnGameCurrentEmotePath, FnVersion fnVersion, UeVersion ueVersion, string ueEmotesPackagePath, string codename, 
        bool useIntro, float loopSoundStartTime)
        {
            string soundCueName = $"SC_EmoteMusic_{codename}.uasset";
            string soundCue3PName = $"SC_EmoteMusic3P_{codename}.uasset";
            string soundCueUassetPath = Path.Combine(OutputFnGameCurrentEmotePath, "Sound", soundCueName);
            string soundCue3PUassetPath = Path.Combine(OutputFnGameCurrentEmotePath, "Sound", soundCue3PName);

            byte[] soundCueUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteSoundCue" + (useIntro ? "Intro.uasset" : ".uasset"));
            byte[] soundCueUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteSoundCue" + (useIntro ? "Intro.uexp" : ".uexp"));
            byte[] soundCue3PUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteSoundCue3P" + (useIntro ? "Intro.uasset" : ".uasset"));
            byte[] soundCue3PUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "EmoteSoundCue3P" + (useIntro ? "Intro.uexp" : ".uexp"));

            File.WriteAllBytes(soundCueUassetPath, soundCueUassetBase64);
            File.WriteAllBytes(Path.ChangeExtension(soundCueUassetPath, ".uexp"), soundCueUexpBase64);
            File.WriteAllBytes(soundCue3PUassetPath, soundCue3PUassetBase64);
            File.WriteAllBytes(Path.ChangeExtension(soundCue3PUassetPath, ".uexp"), soundCue3PUexpBase64);

            float loopSoundWaveLength =
(           (FloatPropertyData)((NormalExport)new UAsset(Path.Combine(OutputFnGameCurrentEmotePath, "Sound", $"{codename}_Sound_Loop.uasset"), ueVersion.UassetApiEngineVer).Exports[0])["Duration"]).Value;

            var soundCueAsset = new UAsset(soundCueUassetPath, ueVersion.UassetApiEngineVer);
            var exportData = soundCueAsset.Exports;
            var export0 = (NormalExport)exportData[0];

            export0.ObjectName.Value.Value = Path.GetFileNameWithoutExtension(soundCueName);

            var loopSoundWaveAssetPtr = (SoftObjectPropertyData)((NormalExport)exportData[useIntro ? fnVersion.LoopingSoundWaveAssetPtrIndex : 1])["SoundWaveAssetPtr"];
            loopSoundWaveAssetPtr.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Loop.{codename}_Sound_Loop";

            var importData = soundCueAsset.Imports;
            importData[useIntro ? 7 : 4].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Loop";
            importData[useIntro ? 16 : 10].ObjectName.Value.Value = $"{codename}_Sound_Loop";

            if (useIntro)
            {
                var introSoundWaveAssetPtr = (SoftObjectPropertyData)((NormalExport)exportData[fnVersion.IntroSoundWaveAssetPtrIndex])["SoundWaveAssetPtr"];
                introSoundWaveAssetPtr.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Intro.{codename}_Sound_Intro";

                var delayMin = (FloatPropertyData)((NormalExport)exportData[1])["DelayMin"];
                var delayMax = (FloatPropertyData)((NormalExport)exportData[1])["DelayMax"];

                delayMin.Value = loopSoundStartTime;
                delayMax.Value = loopSoundStartTime;

                importData[6].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Intro";
                importData[15].ObjectName.Value.Value = $"{codename}_Sound_Intro";
            }

            soundCueAsset.Write(soundCueUassetPath);



            var soundCue3PAsset = new UAsset(soundCue3PUassetPath, ueVersion.UassetApiEngineVer);
            exportData = soundCue3PAsset.Exports;
            export0 = (NormalExport)exportData[0];
            export0.ObjectName.Value.Value = Path.GetFileNameWithoutExtension(soundCue3PName);

            loopSoundWaveAssetPtr = (SoftObjectPropertyData)((NormalExport)exportData[useIntro ? fnVersion.LoopingSoundWaveAssetPtrIndex : 1])["SoundWaveAssetPtr"];
            loopSoundWaveAssetPtr.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Loop.{codename}_Sound_Loop";

            importData = soundCue3PAsset.Imports;
            importData[useIntro ? 8 : 5].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Loop";
            importData[useIntro ? 18 : 12].ObjectName.Value.Value = $"{codename}_Sound_Loop";

            if (useIntro)
            {
                var introSoundWaveAssetPtr = (SoftObjectPropertyData)((NormalExport)exportData[fnVersion.IntroSoundWaveAssetPtrIndex])["SoundWaveAssetPtr"];
                introSoundWaveAssetPtr.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Intro.{codename}_Sound_Intro";

                var delayMin = (FloatPropertyData)((NormalExport)exportData[1])["DelayMin"];
                var delayMax = (FloatPropertyData)((NormalExport)exportData[1])["DelayMax"];

                delayMin.Value = loopSoundStartTime;
                delayMax.Value = loopSoundStartTime;

                importData[7].ObjectName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Sound/{codename}_Sound_Intro";
                importData[17].ObjectName.Value.Value = $"{codename}_Sound_Intro";
            }

            soundCue3PAsset.Write(soundCue3PUassetPath);
        }

        internal static void CreateEid(string OutputFnGamePath, FnVersion fnVersion, UeVersion ueVersion, string ueEmotesPackagePath, string codename, 
        string eid, string name, string description, string rarity, string series)
        {
            string eidUassetName = $"{eid}.uasset";
            string eidUassetPath = Path.Combine(OutputFnGamePath, "Content", "Athena", "Items", "Cosmetics", "Dances", eidUassetName);
            if (!Path.Exists(Path.GetDirectoryName(eidUassetPath))) Directory.CreateDirectory(Path.GetDirectoryName(eidUassetPath));

            byte[] eidUassetBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Eid.uasset");
            byte[] eidUexpBase64 = TemplateLoader.GetEmbeddedFile(fnVersion.Name, "CookedUeAssets", "Eid.uexp");

            File.WriteAllBytes(eidUassetPath, eidUassetBase64);
            File.WriteAllBytes(Path.ChangeExtension(eidUassetPath, ".uexp"), eidUexpBase64);

            var eidAsset = new UAsset(eidUassetPath, ueVersion.UassetApiEngineVer);
            var exportData = eidAsset.Exports;
            var export0 = (NormalExport)exportData[0];
            var importData = eidAsset.Imports;

            export0.ObjectName.Value.Value = eid;
            var animation = (SoftObjectPropertyData)export0["Animation"];
            var animationFemaleOverride = (SoftObjectPropertyData)export0["AnimationFemaleOverride"];
            animation.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Animations/Emote_{codename}_CMM_M.Emote_{codename}_CMM_M";
            animationFemaleOverride.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/Animations/Emote_{codename}_CMF_M.Emote_{codename}_CMF_M";

            var smallPreviewImage = (SoftObjectPropertyData)export0["SmallPreviewImage"];
            var largePreviewImage = (SoftObjectPropertyData)export0["LargePreviewImage"];
            smallPreviewImage.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/UI/T-Icon-Emotes-E-{codename}.T-Icon-Emotes-E-{codename}";
            largePreviewImage.Value.AssetPath.AssetName.Value.Value = $"{ueEmotesPackagePath}/{codename}/UI/T-Icon-Emotes-E-{codename}-L.T-Icon-Emotes-E-{codename}-L";

            ((TextPropertyData)export0["DisplayName"]).CultureInvariantString.Value = name;
            Console.WriteLine($"Changed the DisplayName in {eid} to {name}");
            ((TextPropertyData)export0["Description"]).CultureInvariantString.Value = description;
            Console.WriteLine($"Changed the Description in {eid} to {description}");
            string displayNameKey = Guid.NewGuid().ToString("N").ToUpper(); //Generates a new key for the display name since multiple display names can't use the same key
            string descriptionKey = Guid.NewGuid().ToString("N").ToUpper();
            ((TextPropertyData)export0["DisplayName"]).Value.Value = displayNameKey;
            ((TextPropertyData)export0["Description"]).Value.Value = descriptionKey;


            if (series == "None") export0.Data.RemoveAt(10);
            else
            {
                string seriesCodename = SkinAssetCreator.SeriesCodenames.GetValueOrDefault(series) ?? series;
                importData[2].ObjectName.Value.Value = seriesCodename;
                importData[3].ObjectName.Value.Value = $"/Game/Athena/Items/Cosmetics/Series/{seriesCodename}";
                Console.WriteLine($"Changed the Series in {eid} to {series}");
            }

            export0.Data.RemoveAt(7); //Removes gameplay tags

            var rarityProperty = (EnumPropertyData)export0["Rarity"];
            rarityProperty.Value.Value.Value = $"EFortRarity::{rarity}";
            if (rarity == "Uncommon") export0.Data.RemoveAt(3); //Removes the rarity property since no rarity is equal to uncommon in fn
            else if (rarity == "Unattainable (Impossible T7)") rarityProperty.Value.Value.Value = $"EFortRarity::Unattainable";
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
                rarityProperty.Value.Value.Value = $"EFortRarity::{rarityCodename}";
            }

            Console.WriteLine($"Changed the Rarity in {eid} to {rarity}");
            eidAsset.Write(eidUassetPath);
        }
    }
}
