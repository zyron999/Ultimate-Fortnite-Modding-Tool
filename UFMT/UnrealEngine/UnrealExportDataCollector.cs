using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using UFMT.FnAssets;
using UFMT.UI;
using Microsoft.UI.Composition;
using UFMT.Core;

namespace UFMT.UnrealEngine
{
    internal static class UnrealExportDataCollector
    {
        internal static UnrealExportSkinData CollectSkinData(string smallIcon, string largeIcon, ObservableCollection<Material> materials, string texturesPath, bool manuallySwizzleMaterials, string sourcePath, 
        string lobbyAnimationFbx, string lobbyAnimationJson, List<CharacterPart> characterParts, string skinGender, string codename, string CID, string ueSkinsPackagePath)
        {
            try
            {
                List<string> diffuseTexturePaths = new();
                List<string> maskTexturePaths = new();
                List<string> normalTexturePaths = new();
                List<string> specularTexturePaths = new();
                List<string> iconTexturePaths = new();

                if (smallIcon != string.Empty && largeIcon != string.Empty)
                {
                    iconTexturePaths.Add(Path.Combine(sourcePath, "Textures", $"{smallIcon}.png"));
                    iconTexturePaths.Add(Path.Combine(sourcePath, "Textures", $"{largeIcon}.png"));
                }
                else iconTexturePaths = new() { string.Empty, string.Empty };

                foreach (Material mat in materials)
                {
                    diffuseTexturePaths.Add(Path.Combine(texturesPath, $"{mat.SelectedDiffuse}.png"));
                    maskTexturePaths.Add(Path.Combine(texturesPath, $"{mat.SelectedMask}.png"));
                    normalTexturePaths.Add(Path.Combine(texturesPath, $"{mat.SelectedNormal}.png"));

                    if (manuallySwizzleMaterials && mat.Swizzle) specularTexturePaths.Add(Path.Combine(texturesPath, "Swizzled", $"{mat.SelectedSpecular}.png"));
                    else specularTexturePaths.Add(Path.Combine(texturesPath, $"{mat.SelectedSpecular}.png"));
                }
                Path.Combine(sourcePath, "Fbx", $"{lobbyAnimationFbx}.fbx");

                string lobbyAnimationFbxFilePath = Path.Combine(sourcePath, "Fbx", "Lobby_Animation", $"{lobbyAnimationFbx}.fbx");
                var unrealData = new UnrealExportSkinData()
                {
                    FbxPaths = characterParts.Select(cp => $"{cp.FbxPath}.fbx").ToList(),
                    PhysicsMeshNames = characterParts.Where(cp => cp.PhysicsAssetJsonPaths.Count > 0).ToList().Select(cp => Path.GetFileNameWithoutExtension(cp.FbxPath)).ToList(),
                    PhysicsAssetsPaths = characterParts.Select(cp => cp.PhysicsAssetJsonPaths).ToList(),
                    DiffuseTextures = diffuseTexturePaths,
                    MaskTextures = maskTexturePaths,
                    NormalTextures = normalTexturePaths,
                    SpecularTextures = specularTexturePaths,
                    IconTextures = iconTexturePaths,
                    Materials = materials.Select(mat => mat.Name).ToList(),
                    Codename = codename,
                    MeshNames = characterParts.Select(cp => Path.GetFileNameWithoutExtension(cp.FbxPath)).ToList(),
                    CID = CID,
                    LobbyAnimationFbxPath = Path.Exists(lobbyAnimationFbxFilePath) ? lobbyAnimationFbxFilePath : string.Empty,
                    RetargetSource = skinGender == "Male" ? "MPR_SK_M_MALE_Base_Skeleton" : "SK_M_Female_Base_Skeleton",
                    LobbyAnimationJsonPath = string.IsNullOrEmpty(lobbyAnimationJson) ? string.Empty :
                    Path.Combine(sourcePath, "Lobby_Animation", $"{lobbyAnimationJson}.json"),
                    HeadMeshName = Path.GetFileNameWithoutExtension(characterParts.FirstOrDefault(cp => cp.Type == "Head").FbxPath),
                    HatMeshName = Path.GetFileNameWithoutExtension(characterParts.FirstOrDefault(cp => cp.Type == "Hat")?.FbxPath),
                    CharmMeshName = Path.GetFileNameWithoutExtension(characterParts.FirstOrDefault(cp => cp.Type == "Charm")?.FbxPath),
                    CurrentFnVersion = App.Settings.FnVersion,
                    UeSkinsPackagePath = ueSkinsPackagePath
                };

                Log.Test($"The hat is {unrealData.HatMeshName}");
                unrealData.MeshNames.ForEach(mesh => Log.Test($"Current mesh name: {mesh}"));
                return unrealData;
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while trying to collect UnrealExportSkinData! {ex.Message}");
                return null;
            }
        }

        internal static UnrealExportEmoteData CollectEmoteData(EmoteData currentEmote, string emotePackagePath, string unrealEngineVersion)
        {
            try
            {
                List<string> iconTexturePaths = new List<string>();
                iconTexturePaths.Add(Path.Combine(currentEmote.IconsPath, currentEmote.SmallIcon));
                iconTexturePaths.Add(Path.Combine(currentEmote.IconsPath, currentEmote.LargeIcon));
                if (currentEmote.SmallIcon == string.Empty) iconTexturePaths[0] = string.Empty;
                if (currentEmote.LargeIcon == string.Empty) iconTexturePaths[1] = string.Empty;

                var unrealData = new UnrealExportEmoteData()
                {
                    MaleAnimationFbxPath = Path.Combine(currentEmote.SourcePath, "Fbx", "Animations", currentEmote.MaleAnimationFbx),
                    MaleAnimationJsonPath = Path.Combine(currentEmote.AnimationsPath, currentEmote.MaleAnimationJson),
                    MaleAnimationLength = currentEmote.MaleAnimationLength,
                    FemaleAnimationFbxPath = Path.Combine(currentEmote.SourcePath, "Fbx", "Animations", currentEmote.FemaleAnimationFbx),
                    FemaleAnimationJsonPath = Path.Combine(currentEmote.AnimationsPath, currentEmote.FemaleAnimationJson),
                    FemaleAnimationLength = currentEmote.FemaleAnimationLength,
                    LoopSoundFilePath = Path.Combine(currentEmote.LoopSoundFolderPath, currentEmote.LoopSoundFileName),
                    IntroSoundFilePath = Path.Combine(currentEmote.IntroSoundFolderPath, currentEmote.IntroSoundFileName),
                    SoundWavCompressionQuality = currentEmote.SoundWavCompressionQuality,
                    IconTexturePaths = iconTexturePaths.ToArray(),
                    Codename = currentEmote.Codename,
                    EID = currentEmote.EID,
                    UeEmotesPackagePath = emotePackagePath,
                    UnrealEngineVersion = unrealEngineVersion,
                    LoopSoundStartTime = (float)currentEmote.LoopSoundStartTime
                };

                if (currentEmote.MaleAnimationJson == string.Empty) unrealData.MaleAnimationJsonPath = string.Empty;
                if (currentEmote.FemaleAnimationJson == string.Empty) unrealData.FemaleAnimationJsonPath = string.Empty;
                return unrealData;
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while trying to collect UnrealExportEmoteData! {ex.Message}");
                return null;
            }
        }
    }
}
