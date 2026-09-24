using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UFMT.AssetRegistry;
using UFMT.Blender;
using UFMT.Core;
using UFMT.FnAssets;
using UFMT.FnAssetsLogic;
using UFMT.MaterialTextureAssignment;
using UFMT.u4Pak;
using UFMT.UnrealEngine;
using Windows.Foundation.Collections;

namespace UFMT.UI
{
    public sealed partial class SkinsPage : Page, INotifyPropertyChanged
    {
        public static FnVersion CurrentFnVersion = FnVersionsData.FnVersions.GetValueOrDefault(App.Settings.FnVersion);
        public static UeVersion CurrentUeVersion = UeVersionsData.UeVersions.GetValueOrDefault(App.Settings.UeVersion);
        public string PreviouslySelectedSeries = "None";
        private CancellationTokenSource _currentSkinPathDebounce;
        private string OutputFnGamePath = string.Empty;
        private bool IsUpdatingFromCode = false;
        private bool IsLoadingDropdowns = false;
        CharacterPart Body;
        CharacterPart Head;
        CharacterPart FaceAcc;
        CharacterPart Hat;
        public event PropertyChangedEventHandler PropertyChanged;

        private SkinData _currentSkin;
        public SkinData CurrentSkin
        {
            get => _currentSkin;
            set
            {
                if (_currentSkin != value)
                {
                    _currentSkin = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSkin)));
                }
            }
        }

        private bool _allSwizzleCheckBoxValue;
        public bool AllSwizzleCheckBoxValue
        {
            get => _allSwizzleCheckBoxValue;
            set
            {
                if (_allSwizzleCheckBoxValue != value)
                {
                    _allSwizzleCheckBoxValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SkinsPage()
        {
            InitializeComponent();
            seriesComboBox.Items.Clear();
            var seriesOptions = AppSettings.GetValue<ObservableCollection<string>>("AvailableSeries", null);
            if (seriesOptions != null)
            {
                foreach (string series in seriesOptions)
                {
                    seriesComboBox.Items.Add(series);
                }
                // Just in case the .json didn't contain all the default series
                if (!seriesComboBox.Items.Contains("None"))
                {
                    seriesComboBox.Items.Insert(0, "None");
                    Log.Warning("Failed to find 'None' in Settings JSON; added it automatically.");
                }
                foreach (string defaultSeries in SkinAssetCreator.SeriesCodenames.Keys)
                {
                    if (!seriesComboBox.Items.Contains(defaultSeries))
                    {
                        seriesComboBox.Items.Add(defaultSeries);
                        Log.Warning($"Failed to find '{defaultSeries}' in Settings JSON; added it automatically.");
                    }
                }
                if (!seriesComboBox.Items.Contains("+Add"))
                {
                    seriesComboBox.Items.Add("+Add");
                    Log.Warning("Failed to find '+Add' in Settings JSON; added it automatically.");
                }
                AppSettings.SetValue("AvailableSeries", seriesComboBox.Items.ToArray());
            }
            else
            {
                seriesComboBox.Items.Add("None");
                foreach (string series in SkinAssetCreator.SeriesCodenames.Keys)
                {
                    seriesComboBox.Items.Add(series);
                }
                seriesComboBox.Items.Add("+Add");
            }
            seriesComboBox.SelectedIndex = 0;
            SkinsPathTextBox.Text = AppSettings.GetValue("SkinsPath", "");
            CurrentSkinPathTextBox.Text = AppSettings.GetValue("CurrentSkinPath", "");
            ((FrameworkElement)this.Content).Loaded += (s, e) =>
            {
                LoadContent();
            };
            seriesComboBox.Items.VectorChanged += SaveSeries;
        }

        public void LoadContent()
        {
            CurrentFnVersion = FnVersionsData.FnVersions.GetValueOrDefault(App.Settings.FnVersion);
            CurrentUeVersion = UeVersionsData.UeVersions.GetValueOrDefault(App.Settings.UeVersion);
            if (App.Settings.FnVersion == "8.51-9.10" || App.Settings.FnVersion == "9.41")
            {
                Ch1PreviewViewBox.Visibility = Visibility.Visible;
                Ch2PreviewViewBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                Ch1PreviewViewBox.Visibility = Visibility.Collapsed;
                Ch2PreviewViewBox.Visibility = Visibility.Visible;
            }
            Body = new CharacterPart
            {
                Type = "Body",
            };
            Head = new CharacterPart
            {
                Type = "Head",
            };
            FaceAcc = new CharacterPart
            {
                Type = "Faceacc",
            };
            Hat = new CharacterPart
            {
                Type = "Hat",
            };

            if (CurrentUeVersion.ReplaceDefaultEngineIni)
            {
                string defaultEngineIniPath = Path.Combine(Path.GetDirectoryName(App.Settings.UeProjectPath),
                "Config", "DefaultEngine.ini");
                byte[] defaultEngineIniInBytes = TemplateLoader.GetEmbeddedFile(CurrentUeVersion.Name, "RawUeAssets", "DefaultEngine.ini");
                if (defaultEngineIniInBytes != null) File.WriteAllBytes(defaultEngineIniPath, defaultEngineIniInBytes);
            }

            CurrentSkinPathTextBox_TextChanged("NoDelay", null);
        }

        private void SkinsPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("SkinsPath", SkinsPathTextBox.Text);
        }

        private async void CurrentSkinPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("CurrentSkinPath", CurrentSkinPathTextBox.Text);

            _currentSkinPathDebounce?.Cancel();
            _currentSkinPathDebounce = new CancellationTokenSource();
            var token = _currentSkinPathDebounce.Token;

            if (sender as string != "NoDelay")
            {
                try
                {
                    //Wait 250ms, if the user is still typing, this gets cancelled
                    await Task.Delay(250, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
            CurrentSkin = new SkinData();
            UpdateDropdowns();
            if (!SkinValidator.ValidateAfterPathChange(CurrentSkinPathTextBox.Text, CurrentSkin)) return;

            OutputFnGamePath = Path.Combine(CurrentSkin.Path, "Output", App.Settings.FnVersion, "FortniteGame");
            CurrentSkin.Codename = new DirectoryInfo(CurrentSkin.Path).Name;
            CurrentSkin.CID = $"CID_{CurrentSkin.Codename}";

            List<CharacterPart> characterParts = 
            SkinFolderScanner.FindCharacterParts(CurrentSkin.MeshesPath, CurrentSkin.PhysicsPath, new List<CharacterPart>() { Body, Head, FaceAcc, Hat });
            if (characterParts == null) return;
            CurrentSkin.CharacterParts = characterParts;

            (bool isValid, string lobbyAnimationPsa, string lobbyAnimationJson) = SkinFolderScanner.FindLobbyAnimationFiles(CurrentSkin.LobbyAnimationFolderPath);
            if (!isValid) return;
            CurrentSkin.LobbyAnimationPsa = lobbyAnimationPsa;
            CurrentSkin.LobbyAnimationJson = lobbyAnimationJson;

            List<Material> materials =
            PskReader.GetMaterialData(CurrentSkin.CharacterParts.Select(cp => cp.PskPath).ToList(),
            CurrentSkin.CharacterParts, allSwizzleCheckBox.IsChecked.Value, this);

            if (materials == null) return;
            CurrentSkin.Materials = new ObservableCollection<Material>(materials);

            DefaultTextureSetup.CreateDefaultTextures(DefaultTextureSetup.FindMissingDefaultTextures(CurrentSkin.TexturesPath), CurrentSkin.TexturesPath);
            if (CurrentFnVersion.ManuallySwizzleMaterials) TextureSwizzler.SwizzleSpecularTextures(CurrentSkin.TexturesPath);
            (string largeIcon, string smallIcon) = TextureCategorizer.GetIconTextures(CurrentSkin.TexturesPath, "skin");
            if (largeIcon == null || smallIcon == null) return;
            CurrentSkin.LargeIcon = largeIcon;
            CurrentSkin.SmallIcon = smallIcon;

            CurrentSkin.Textures = TextureCategorizer.GetAllTextures(CurrentSkin.TexturesPath);
            MaterialTextureAssigner.AssignTexturesToAllMaterials(CurrentSkin.TexturesPath, CurrentSkin.Codename, CurrentSkin.Materials);
            characterCIDTextBox.Text = CurrentSkin.CID;
            LoadSkinConfigInto(Path.Combine(CurrentSkin.Path, $"{CurrentSkin.Codename}_Settings.json"), CurrentSkin);
            //CurrentSkin.Name = "Testing!";
            UpdateDropdowns();

            CurrentSkin.PropertyChanged += (s, e) => SaveSkinConfig();
            foreach (Material mat in CurrentSkin.Materials) mat.isLoading = false;
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                var picker = new Windows.Storage.Pickers.FolderPicker();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    if (button.Name == "SkinsPathBrowse")
                    {
                        SkinsPathTextBox.Text = folder.Path;
                    }
                    else if (button.Name == "CurrentSkinPathBrowse")
                    {
                        CurrentSkinPathTextBox.Text = folder.Path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private void Reimport_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSkin == null) return;
            string jsonPath = Path.Combine(CurrentSkin.Path, $"{CurrentSkin.Codename}_Settings.json");
            if (Path.Exists(jsonPath)) File.Delete(jsonPath);
            CurrentSkinPathTextBox_TextChanged(null, null);
        }

        private async void RenderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(App.Settings.UeVersion))
            {
                Log.Error($"No unreal engine selected! Make sure you selected the correct ue version in setting!");
                return;
            }

            if (CurrentSkin == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(CurrentSkin.Gender))
            {
                Log.Error($"The skin's gender is unspecified");
                return;
            }
            try
            {
                if (!await BlenderPreviewRenderer.RenderSkinPreviewImage(CurrentSkin.CharacterParts, CurrentSkin.Gender, CurrentSkin.LobbyAnimationFolderPath, CurrentSkin.LobbyAnimationPsa,
                CurrentSkin.Materials, CurrentSkin.Path, CurrentSkin.Codename, CurrentSkin.TexturesPath, App.Settings.FnVersion)) return;
                await UpdateSkinPreviewImage(CurrentSkin.SourcePath, CurrentSkin.Codename, CurrentSkin.LargeIcon);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SkinData exportSkin = CurrentSkin.Clone();
            UeVersion ueVer = CurrentUeVersion;
            FnVersion fnVer = CurrentFnVersion;
            string outputFnGamePath = OutputFnGamePath;
            string ueProjectPath = App.Settings.UeProjectPath;
            string ueExecutablePath = App.Settings.UeExecutablePath;
            string ueSkinsPackagePath = App.Settings.UeSkinsPackagePath;
            string ueEmotesPackagePath = App.Settings.UeEmotesPackagePath;

            if (!SkinValidator.ValidateBeforeExport
            (ueVer.Name, exportSkin.Gender, exportSkin.Name, exportSkin.Description, exportSkin.CID, ueSkinsPackagePath, ueProjectPath, ueExecutablePath)) return;
            if (!await FbxConverter.ConvertPskToFbx(exportSkin.CharacterParts, exportSkin.SourcePath, exportSkin.Codename)) return;

            string cookedAssetsPath = Path.Combine(Path.GetDirectoryName(App.Settings.UeProjectPath),
            "Saved", "Cooked", "WindowsNoEditor", Path.GetFileNameWithoutExtension(App.Settings.UeProjectPath), "Content");
            string ueSkinsOsPath = ueSkinsPackagePath.Substring(6, ueSkinsPackagePath.Length - 6).Replace("/", "\\"); //Remove /Game/ at the start and replace / with \
            string pluginPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Plugins", "PhysicsImporter");
            string physicsImporterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"PhysicsImporter_{ueVer.Name}.zip");

            if (exportSkin.LobbyAnimationPsa != string.Empty)
            {
                bool isAnimValid = await FbxConverter.ConvertPsaToFbx(Path.Combine(exportSkin.LobbyAnimationFolderPath, $"{exportSkin.LobbyAnimationPsa}.psa"),
                Path.Combine(exportSkin.SourcePath, "Fbx", "Lobby_Animation", $"{exportSkin.Codename}_Lobby_Animation.fbx"), true);
                if (!isAnimValid) return;
                exportSkin.LobbyAnimationFbx = $"{exportSkin.Codename}_Lobby_Animation";
                exportSkin.LobbyAnimationLength = (float)PsaReader.GetAnimationLength(Path.Combine(exportSkin.LobbyAnimationFolderPath, $"{exportSkin.LobbyAnimationPsa}.psa")) / 30f;
            }
            string cookedCodenamePath = Path.Combine(cookedAssetsPath, ueSkinsOsPath, exportSkin.Codename);

            if (!await UnrealDependencySetup.AddRequiredUeAssetsBeforeExport(ueProjectPath, ueVer.BaseHeadPath, cookedCodenamePath, ueVer.Name,
            ueVer.BaseHeadFileNames, pluginPath, physicsImporterPath)) return;
            
            UnrealExportSkinData unrealData = UnrealExportDataCollector.CollectSkinData(exportSkin.SmallIcon, exportSkin.LargeIcon, exportSkin.Materials, exportSkin.TexturesPath,
            fnVer.ManuallySwizzleMaterials, exportSkin.SourcePath, exportSkin.LobbyAnimationFbx, exportSkin.LobbyAnimationJson, exportSkin.CharacterParts,
            exportSkin.Gender, exportSkin.Codename, exportSkin.CID, ueSkinsPackagePath);
            if (unrealData == null) return;
            string jsonString = System.Text.Json.JsonSerializer.Serialize(unrealData, AppJsonContext.Default.UnrealExportSkinData);
            await UnrealProcessRunner.LaunchUnreal(jsonString, ueProjectPath, ueExecutablePath, "skin");

            if (!SkinValidator.ValidateAfterUeImport(ueProjectPath, ueSkinsOsPath, exportSkin.Codename, unrealData.DiffuseTextures, unrealData.MaskTextures, unrealData.NormalTextures,
            unrealData.SpecularTextures, unrealData.Materials, unrealData.MeshNames, exportSkin.SmallIcon, exportSkin.LargeIcon, exportSkin.LobbyAnimationFbx, exportSkin.CID))
            {
                Log.Error($"Unreal Engine import process failed!");
                return;
            }

            await UnrealProcessRunner.CookFiles(ueProjectPath, ueExecutablePath);

            if (!SkinValidator.ValidateAfterUeCook(cookedCodenamePath, exportSkin.Codename, unrealData.DiffuseTextures, unrealData.MaskTextures, unrealData.NormalTextures,
            unrealData.SpecularTextures, unrealData.Materials, unrealData.MeshNames, exportSkin.SmallIcon, exportSkin.LargeIcon, exportSkin.LobbyAnimationFbx, exportSkin.CID))
            {
                Log.Error($"Unreal Engine cook process failed!");
                return;
            }

            ueVer.FixRequiredFiles([Path.Combine
            (cookedCodenamePath, "Animations", $"{exportSkin.Codename}_Lobby_Animation.uasset")], exportSkin.CharacterParts.Select
            (cp => Path.Combine(cookedCodenamePath, "Meshes", $"{Path.GetFileNameWithoutExtension(cp.FbxPath)}.uasset")).ToArray());

            AssetRegistryBuilder.CreateAssetRegistry(cookedAssetsPath, ueVer.Name, outputFnGamePath, ueSkinsPackagePath, ueEmotesPackagePath, exportSkin.Path);

            DirectoryInfo cookedCharacterDirectory = new DirectoryInfo(
            Path.Combine(cookedAssetsPath, ueSkinsOsPath, exportSkin.Codename));
            string contentFolderPath = Path.Combine(outputFnGamePath, "Content", ueSkinsOsPath, exportSkin.Codename);

            SkinAssetCreator.CopyFilesFromUe(contentFolderPath, cookedCharacterDirectory, cookedAssetsPath, outputFnGamePath, ueVer.BaseHeadPath, ueVer.ReplaceCookedBaseHead,
            fnVer.Name, ueVer.Name, ueVer.BaseHeadFileNames);

            SkinAssetCreator.CreateCharacterParts(contentFolderPath, exportSkin.Gender, exportSkin.Codename, exportSkin.CharacterParts, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            SkinAssetCreator.CreateMaterials(contentFolderPath, exportSkin.Codename, exportSkin.Materials, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            SkinAssetCreator.CreateHeroSpecialization(contentFolderPath, exportSkin.Codename, exportSkin.CharacterParts, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            SkinAssetCreator.CreateLobbyAnimationMontage(contentFolderPath, exportSkin.Codename, exportSkin.LobbyAnimationPsa, exportSkin.LobbyAnimationJson,
            exportSkin.LobbyAnimationLength, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            SkinAssetCreator.CreateHero(contentFolderPath, exportSkin.Codename, exportSkin.Gender, exportSkin.SmallIcon, exportSkin.LargeIcon, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            SkinAssetCreator.CreateCharacter(outputFnGamePath, exportSkin.CID, exportSkin.Codename, exportSkin.Name, exportSkin.Description, exportSkin.Rarity,
            exportSkin.Series, fnVer, ueVer.UassetApiEngineVer, ueSkinsPackagePath);

            U4Pak.Pack(outputFnGamePath, Path.Combine(Path.GetDirectoryName(outputFnGamePath), $"z_{exportSkin.Codename}.pak"));
            Log.Success("\nYour custom skin is ready! Check the output folder");
        }

        private void SmallIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentSkin == null || CurrentSkin.SmallIcon == null || (sender as ComboBox)?.Items == null) return;
            CurrentSkin.SmallIcon = (sender as ComboBox).SelectedItem?.ToString();
            Console.WriteLine($"Changed the small icon to {CurrentSkin.SmallIcon}");
        }
        private void LargeIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentSkin == null || CurrentSkin.LargeIcon == null || (sender as ComboBox)?.Items == null) return;
            CurrentSkin.LargeIcon = (sender as ComboBox).SelectedItem?.ToString();
            Console.WriteLine($"Changed the large icon to {CurrentSkin.LargeIcon}");
        }
        private async void CreateSkinFolder_Click
        (object sender, RoutedEventArgs e)
        {
            CreateFolderDialog.XamlRoot = this.Content.XamlRoot;

            if (SkinsPathTextBox.Text == null || SkinsPathTextBox.Text == "")
            {
                Log.Error("The skins path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(SkinsPathTextBox.Text))
            {
                Log.Error($"\"{SkinsPathTextBox.Text}\" doesn't exist!");
                return;
            }

            CodenameFolderCreateTextBox.Text = "";
            var result = await CreateFolderDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newName = CodenameFolderCreateTextBox.Text;
                string rootPath = SkinsPathTextBox.Text;

            }
        }

        private void CreateFolderDialog_SecondaryButtonClick
        (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (SkinsPathTextBox.Text == null || SkinsPathTextBox.Text == "")
            {
                Log.Error("The skins path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(SkinsPathTextBox.Text))
            {
                Log.Error($"\"{SkinsPathTextBox.Text}\" doesn't exist!");
                return;
            }

            if (CodenameFolderCreateTextBox.Text.Length > 30)
            {
                Log.Error("The codename cannot be longer than 30 characters!");
                return;
            }

            if (CodenameFolderCreateTextBox.Text.ToString() == string.Empty)
            {
                Log.Error("The codename cannot be empty!");
                return;
            }

            if (CodenameFolderCreateTextBox.Text.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            {
                Log.Error("The codename can only contain alphabetical characters, numbers and _");
                return;
            }

            Directory.CreateDirectory(Path.Combine(SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text));
            
            string[] cpTypes = {"Body", "Head", "Faceacc", "Hat" };
            string[] cpTypeFolders = {"Meshes", "Physics" };
            foreach (string cpType in cpTypes)
            {
                foreach (string cpTypeFolder in cpTypeFolders)
                {
                    Directory.CreateDirectory(Path.Combine
                    (SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", cpTypeFolder, cpType));
                }
            }

            Directory.CreateDirectory(Path.Combine
            (SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Textures"));

            Directory.CreateDirectory(Path.Combine
            (SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Lobby_Animation"));

            Directory.CreateDirectory(Path.Combine(SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Fbx"));
            Directory.CreateDirectory(Path.Combine(SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Fbx", "Body"));
            Directory.CreateDirectory(Path.Combine(SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Fbx", "Head"));

            Log.Success($"Successfully created skin folder {CodenameFolderCreateTextBox.Text} at {SkinsPathTextBox.Text}");
            System.Diagnostics.Process.Start("explorer.exe", Path.Combine(SkinsPathTextBox.Text, CodenameFolderCreateTextBox.Text));
            args.Cancel = false;
        }

        private void AddSeriesDialog_SecondaryButtonClick
        (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (AddSeriesTextBox.Text.ToString() == string.Empty)
            {
                Log.Error("The series' codename cannot be empty!");
                return;
            }
            if (AddSeriesTextBox.Text.Length > 100)
            {
                Log.Error("The codename cannot be longer than 100 characters!");
                return;
            }
            if (seriesComboBox.Items.Contains(AddSeriesTextBox.Text.ToString()))
            {
                Log.Error($"{AddSeriesTextBox.Text} already exists!");
                return;
            }

            int addIndex = seriesComboBox.Items.IndexOf("+Add");
            if (addIndex != -1)
            {
                seriesComboBox.Items.Insert(addIndex, AddSeriesTextBox.Text);
            }
            else
            {
                seriesComboBox.Items.Add(AddSeriesTextBox.Text);
            }
            Console.WriteLine($"Added {AddSeriesTextBox.Text}");
            seriesComboBox.SelectedItem = AddSeriesTextBox.Text;
            Console.WriteLine($"Selected {AddSeriesTextBox.Text}");
            PreviouslySelectedSeries = AddSeriesTextBox.Text;
            args.Cancel = false;
        }

        private void NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsNaN(args.NewValue) || double.IsInfinity(args.NewValue) || args.NewValue < 0)
            {
                sender.Value = 0;
            }
        }

        private void AllSwizzleChecked(object sender, RoutedEventArgs e)
        {
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;
            foreach (Material mat in CurrentSkin.Materials)
            {
                mat.Swizzle = true;
            }
            IsUpdatingFromCode = false;
        }

        private void AllSwizzleUnchecked(object sender, RoutedEventArgs e)
        {
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;
            foreach (Material mat in CurrentSkin.Materials)
            {
                mat.Swizzle = false;
            }
            IsUpdatingFromCode = false;
        }

        public void UpdateAllSwizzleCheckBoxState()
        {
            if (CurrentSkin.Materials == null || CurrentSkin.Materials.Count == 0) return;
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;

            bool allChecked = CurrentSkin.Materials.All(m => m.Swizzle);
            AllSwizzleCheckBoxValue = allChecked;

            IsUpdatingFromCode = false;
        }

        private async void ComboBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox c = sender as ComboBox;
            if (IsUpdatingFromCode || IsLoadingDropdowns) return;

            if (c.Tag.ToString() == "series")
            {
                string selectedSeries = seriesComboBox.SelectedItem.ToString();
                if (selectedSeries != "None" && !SkinAssetCreator.SeriesCodenames.Keys.Contains(selectedSeries))
                {
                    RemoveSeriesButton.Visibility = Visibility.Visible;
                }
                else
                {
                    RemoveSeriesButton.Visibility = Visibility.Collapsed;
                }
            }

            if (CurrentSkin == null) return;
            if (c.Tag != null)
            {
                if (c.Tag.ToString() == "gender")
                {
                    if (CurrentSkin.Gender != null || c?.SelectedItem == null) return;
                    CurrentSkin.Gender = c.SelectedItem.ToString();
                }

                if (c.Tag.ToString() == "series" && c.SelectedItem.ToString() == "+Add")
                {
                    if (e.RemovedItems.Count > 0)
                    {
                        PreviouslySelectedSeries = e.RemovedItems[0].ToString();
                    }
                    AddSeriesDialog.XamlRoot = this.Content.XamlRoot;
                    AddSeriesTextBox.Text = "";
                    var result = await AddSeriesDialog.ShowAsync();
                }


                if (App.Settings.FnVersion == "8.51-9.10" || App.Settings.FnVersion == "9.41")
                {
                    if (c.Tag.ToString() == "series")
                    {
                        string fullPath = $"ms-appx:///Assets/{CurrentSkin.Series}_Icon_Background.png";

                        if (c.SelectedItem.ToString() == "None")
                        {
                            fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}.png";
                            iconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));

                            fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}_Icon_Overlay.png";
                            RarityIconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));

                            fullPath = $"ms-appx:///Assets/{CurrentSkin.Rarity}_Text.png";
                        }

                        else
                        {
                            fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Series}.png";
                            iconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));

                            fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Series}_Icon_Overlay.png";
                            RarityIconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));


                            fullPath = $"ms-appx:///Assets/{CurrentSkin.Series}_Text.png";
                        }

                    }
                    else if (c?.Tag.ToString() == "rarity" && c?.SelectedItem != null && seriesComboBox?.SelectedItem != null)
                    {
                        if (CurrentSkin.Series != "None") return;
                        string fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}.png";
                        iconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));

                        fullPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}_Icon_Overlay.png";
                        RarityIconOverlayCh1.Source = new BitmapImage(new Uri(fullPath));

                        fullPath = $"ms-appx:///Assets/{CurrentSkin.Rarity}_Text.png";
                    }
                }
                else
                {
                    Ch1PreviewViewBox.Visibility = Visibility.Collapsed;
                    Ch2PreviewViewBox.Visibility = Visibility.Visible;
                    if (c.Tag.ToString() == "series")
                    {
                        string fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Series}_Icon_Background.png";
                        iconBackgroundOverlay.Source = new BitmapImage(new Uri(fullPath));

                        if (c.SelectedItem.ToString() == "None")
                        {
                            fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Icon.png";
                            iconOverlay.Source = new BitmapImage(new Uri(fullPath));

                            fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Text.png";
                            textOverlay.Source = new BitmapImage(new Uri(fullPath));
                        }

                        else
                        {
                            fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Series}_Icon.png";
                            iconOverlay.Source = new BitmapImage(new Uri(fullPath));

                            fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Series}_Text.png";
                            textOverlay.Source = new BitmapImage(new Uri(fullPath));
                        }

                    }
                    else if (c?.Tag.ToString() == "rarity" && c?.SelectedItem != null && seriesComboBox?.SelectedItem != null)
                    {
                        if (CurrentSkin.Series != "None") return;
                        string fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Icon.png";
                        iconOverlay.Source = new BitmapImage(new Uri(fullPath));

                        fullPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Text.png";
                        textOverlay.Source = new BitmapImage(new Uri(fullPath));
                    }
                }
            }
        }

        private void AddSeriesDialogClosed(object sender, ContentDialogClosedEventArgs e)
        {
            seriesComboBox.SelectedItem = PreviouslySelectedSeries;
        }
        private void RemoveSeriesButton_Click(object sender, RoutedEventArgs e)
        {
            string itemToDelete = seriesComboBox.SelectedItem.ToString();
            seriesComboBox.SelectedItem = "None";
            seriesComboBox.Items.Remove(itemToDelete);
            Console.WriteLine($"Deleted \"{itemToDelete}\"");
        }
        private void CharacterTextBoxChanged(object sender, RoutedEventArgs e)
        {
            var c = sender as TextBox;
            string characterTextBoxType = c?.Tag?.ToString();
            if (characterTextBoxType == null || CurrentSkin == null) return;
            if (characterTextBoxType == "characterName")
            {
                CurrentSkin.Name = c.Text;
                characterNameText.Text = CurrentSkin.Name.ToUpper();
                characterNameTextCh1.Text = CurrentSkin.Name.ToUpper();
            }
            else if (characterTextBoxType == "characterDescription")
            {
                CurrentSkin.Description = c.Text;
            }
            else if (characterTextBoxType == "characterCID")
            {
                CurrentSkin.CID = c.Text;
            }
        }

        private void UpdateDropdowns()
        {
            try
            {
                IsLoadingDropdowns = true;

                foreach (Material mat in CurrentSkin.Materials)
                {
                    mat.TextureOptions = CurrentSkin.Textures;
                    if (!mat.TextureOptions.Contains(mat.SelectedDiffuse))
                    {
                        Log.Error($"Diffuse texture '{mat.SelectedDiffuse}' on material '{mat.Name}' saved in skin's settings does not exist");
                        mat.SelectedDiffuse = "Default_Diffuse";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedMask))
                    {
                        Log.Error($"Mask texture '{mat.SelectedMask}' on material '{mat.Name}' saved in skin's settings does not exist");
                        mat.SelectedMask = "Default_Mask";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedNormal))
                    {
                        Log.Error($"Normal texture '{mat.SelectedNormal}' on material '{mat.Name}' saved in skin's settings does not exist");
                        mat.SelectedNormal = "Default_Normal";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedSpecular))
                    {
                        Log.Error($"Specular texture '{mat.SelectedSpecular}' on material '{mat.Name}' saved in skin's settings does not exist");
                        mat.SelectedSpecular = "Default_Specular";
                    }
                }

                DynamicExpanderList.ItemsSource = CurrentSkin.Materials;

                void OnLayoutUpdated(object s, object e)
                {
                    DynamicExpanderList.LayoutUpdated -= OnLayoutUpdated;
                    IsLoadingDropdowns = false;
                    string imgPath;
                    if (App.Settings.FnVersion == "8.51-9.10" || App.Settings.FnVersion == "9.41")
                    {
                        if (CurrentSkin.Series == "None")
                        {
                            imgPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}.png";
                            iconOverlayCh1.Source = new BitmapImage(new Uri(imgPath));

                            imgPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Rarity}_Icon_Overlay.png";
                            RarityIconOverlayCh1.Source = new BitmapImage(new Uri(imgPath));
                        }
                        else
                        {
                            imgPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Series}.png";
                            iconOverlayCh1.Source = new BitmapImage(new Uri(imgPath));

                            imgPath = $"ms-appx:///Assets/Chapter1/{CurrentSkin.Series}_Icon_Overlay.png";
                            RarityIconOverlayCh1.Source = new BitmapImage(new Uri(imgPath));
                        }
                    }
                    else
                    {
                        if (CurrentSkin.Series == "None")
                        {
                            imgPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Icon.png";
                            iconOverlay.Source = new BitmapImage(new Uri(imgPath));

                            imgPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Rarity}_Text.png";
                            textOverlay.Source = new BitmapImage(new Uri(imgPath));
                        }

                        else
                        {
                            imgPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Series}_Icon.png";
                            iconOverlay.Source = new BitmapImage(new Uri(imgPath));

                            imgPath = $"ms-appx:///Assets/Chapter2/{CurrentSkin.Series}_Text.png";
                            textOverlay.Source = new BitmapImage(new Uri(imgPath));
                        }
                    }

                    if (!CurrentSkin.Materials.Any(mat => !mat.Swizzle) && CurrentSkin.Materials.Count > 0) AllSwizzleCheckBoxValue = true;
                    else { IsUpdatingFromCode = true; AllSwizzleCheckBoxValue = false; IsUpdatingFromCode = false; };
                }
                DynamicExpanderList.LayoutUpdated += OnLayoutUpdated;
                Log.Success("Updated the dropdowns!");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while trying to update dropdowns! {ex}");
            }
            SmallIconComboBox.Items.Clear();
            LargeIconComboBox.Items.Clear();
            foreach (string texture in CurrentSkin.Textures)
            {
                SmallIconComboBox.Items.Add(texture);
                LargeIconComboBox.Items.Add(texture);
            }
            SmallIconComboBox.SelectedItem = CurrentSkin.SmallIcon;
            LargeIconComboBox.SelectedItem = CurrentSkin.LargeIcon;
        }

        private async Task UpdateSkinPreviewImage(string sourcePath, string codename, string largeIcon)
        {
            var bitmap = new BitmapImage { CreateOptions = BitmapCreateOptions.IgnoreImageCache };
            using (var fileStream = File.OpenRead(Path.Combine(sourcePath, $"{codename}.png")))
            {
                var inMemoryStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await WindowsRuntimeStreamExtensions.AsStreamForWrite(inMemoryStream).WriteAsync(
                    await File.ReadAllBytesAsync(Path.Combine(sourcePath, $"{codename}.png"))
                );
                inMemoryStream.Seek(0);

                await bitmap.SetSourceAsync(inMemoryStream);
                characterPreview.Source = bitmap;
                characterPreviewCh1.Source = bitmap;
            }

            if (!string.IsNullOrEmpty(largeIcon))
            {
                var iconBitmap = new BitmapImage { CreateOptions = BitmapCreateOptions.IgnoreImageCache };
                string iconPath = Path.Combine(sourcePath, "Textures", $"{largeIcon}.png");

                var inMemoryStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                byte[] fileBytes = await File.ReadAllBytesAsync(iconPath);
                using (var dataWriter = new Windows.Storage.Streams.DataWriter(inMemoryStream))
                {
                    dataWriter.WriteBytes(fileBytes);
                    await dataWriter.StoreAsync();
                    await dataWriter.FlushAsync();
                    dataWriter.DetachStream();
                }
                inMemoryStream.Seek(0);

                await iconBitmap.SetSourceAsync(inMemoryStream);
                iconPreview.Source = iconBitmap;
                iconPreviewCh1.Source = iconBitmap;
            }
            Console.WriteLine("Successfully updated the preview image!");
        }

        private void LoadSkinConfigInto(string jsonPath, SkinData target)
        {
            if (!File.Exists(jsonPath) || target == null) return;

            string jsonString = File.ReadAllText(jsonPath);
            var node = System.Text.Json.Nodes.JsonNode.Parse(jsonString)?.AsObject();
            if (node == null) return;

            if (node.ContainsKey("CodeName") && !node.ContainsKey("Codename"))
            {
                var value = node["CodeName"];
                node.Remove("CodeName");
                node["Codename"] = value;
            }

            if (node.ContainsKey("Series") && node["Series"]?.ToString() != "None")
            {
                string currentSeries = node["Series"].ToString();
                if (currentSeries == "+Add")
                {
                    node["Series"] = "None";
                }
                else if (!seriesComboBox.Items.Contains(currentSeries))
                {
                    seriesComboBox.Items.Insert(seriesComboBox.Items.Count - 1, currentSeries);
                    Console.WriteLine($"Detected new series on the loaded skin, added \"{currentSeries}\"");
                }
            }

            if (node.ContainsKey("CharacterParts") && node["CharacterParts"] is System.Text.Json.Nodes.JsonArray parts)
            {
                foreach (var part in parts)
                {
                    if (part is System.Text.Json.Nodes.JsonObject partObj)
                    {
                        if (partObj.ContainsKey("Type"))
                        {
                            string typeValue = partObj["Type"]?.ToString();
                            if (!string.IsNullOrEmpty(typeValue))
                            {
                                partObj["Type"] = char.ToUpper(typeValue[0]) + typeValue.Substring(1);
                            }
                        }

                        if (partObj.ContainsKey("PskPath"))
                        {
                            string pathValue = partObj["PskPath"]?.ToString();
                            partObj.Remove("PskPath");
                            partObj["Psk"] = !string.IsNullOrEmpty(pathValue) ? System.IO.Path.GetFileNameWithoutExtension(pathValue) : "";
                        }

                        if (partObj.ContainsKey("PhysicsAssetJsonPaths"))
                        {
                            string[] jsonNames = partObj["PhysicsAssetJsonPaths"]?.AsArray().Select(json => Path.GetFileNameWithoutExtension(json.ToString())).ToArray();
                            partObj.Remove("PhysicsAssetJsonPaths");
                            partObj["PhysicsAssets"] = System.Text.Json.JsonSerializer.SerializeToNode(jsonNames);
                        }
                    }
                }
            }

            var loadedSkin = System.Text.Json.JsonSerializer.Deserialize<SkinData>(node.ToJsonString());
            if (loadedSkin == null) return;

            foreach (var prop in typeof(SkinData).GetProperties())
            {
                if (!prop.CanWrite || prop.IsDefined(typeof(JsonIgnoreAttribute), false) || prop.Name == "Materials")
                    continue;

                var val = prop.GetValue(loadedSkin);
                prop.SetValue(target, val);
            }

            try
            {
                if (target.Materials != null && loadedSkin.Materials != null)
                {
                    foreach (var existingMat in target.Materials)
                    {
                        existingMat.ParentPage = this;
                        var jsonMat = loadedSkin.Materials.FirstOrDefault(mat => mat?.Name == existingMat.Name);
                        if (jsonMat != null)
                        {
                            existingMat.Swizzle = jsonMat.Swizzle;
                            existingMat.UseSkinBoostColor = jsonMat.UseSkinBoostColor;
                            existingMat.SbcRed = jsonMat.SbcRed;
                            existingMat.SbcBlue = jsonMat.SbcBlue;
                            existingMat.SbcGreen = jsonMat.SbcGreen;
                            existingMat.SbcAlpha = jsonMat.SbcAlpha;

                            string jsonSelectedDiffuse = jsonMat.JsonSelectedDiffuse ?? jsonMat.SelectedDiffuse; // for old jsons that still contain SelectedDiffuse
                            if (target.Textures.Contains(jsonSelectedDiffuse))
                            {
                                existingMat.SelectedDiffuse = jsonSelectedDiffuse;
                                existingMat.JsonSelectedDiffuse = jsonSelectedDiffuse;
                            }
                            else
                            {
                                Log.Error($"Diffuse texture '{jsonSelectedDiffuse}' on material '{jsonMat.Name}' saved in settings JSON does not exist.");
                                existingMat.SelectedDiffuse = "Default_Diffuse";
                                existingMat.JsonSelectedDiffuse = jsonSelectedDiffuse;
                            }

                            string jsonSelectedMask = jsonMat.JsonSelectedMask ?? jsonMat.SelectedMask;
                            if (target.Textures.Contains(jsonSelectedMask))
                            {
                                existingMat.SelectedMask = jsonSelectedMask;
                                existingMat.JsonSelectedMask = jsonSelectedMask;
                            }
                            else
                            {
                                Log.Error($"Mask texture '{jsonSelectedMask}' on material '{jsonMat.Name}' saved in settings JSON does not exist.");
                                existingMat.SelectedMask = "Default_Mask";
                                existingMat.JsonSelectedMask = jsonSelectedMask;
                            }

                            string jsonSelectedNormal = jsonMat.JsonSelectedNormal ?? jsonMat.SelectedNormal;
                            if (target.Textures.Contains(jsonSelectedNormal))
                            {
                                existingMat.SelectedNormal = jsonSelectedNormal;
                                existingMat.JsonSelectedNormal = jsonSelectedNormal;
                            }
                            else
                            {
                                Log.Error($"Normal texture '{jsonSelectedNormal}' on material '{jsonMat.Name}' saved in settings JSON does not exist.");
                                existingMat.SelectedNormal = "Default_Normal";
                                existingMat.JsonSelectedNormal = jsonSelectedNormal;
                            }

                            string jsonSelectedSpecular = jsonMat.JsonSelectedSpecular ?? jsonMat.SelectedSpecular;
                            if (target.Textures.Contains(jsonSelectedSpecular))
                            {
                                existingMat.SelectedSpecular = jsonSelectedSpecular;
                                existingMat.JsonSelectedSpecular = jsonSelectedSpecular;
                            }
                            else
                            {
                                Log.Error($"Specular texture '{jsonSelectedSpecular}' on material '{jsonMat.Name}' saved in settings JSON does not exist.");
                                existingMat.SelectedSpecular = "Default_Specular";
                                existingMat.JsonSelectedSpecular = jsonSelectedSpecular;
                            }
                        }
                        else
                        {
                            Log.Error($"Material {existingMat.Name} was not found in skin's settings!");
                        }
                    }
                }

                if (CurrentFnVersion.ManuallySwizzleMaterials) TextureSwizzler.SwizzleSpecularTextures(CurrentSkin.TexturesPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private static readonly JsonSerializerOptions SaveOptions = new()
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
        {
            typeInfo =>
            {
                if (typeInfo.Type != typeof(Material)) return;
                foreach (var prop in typeInfo.Properties)
                {
                    if (prop.Name is "SelectedDiffuse" or "SelectedMask"
                                  or "SelectedNormal" or "SelectedSpecular")
                        prop.ShouldSerialize = (_, _) => false;
                }
            }
        }
            }
        };
        public void SaveSkinConfig()
        {
            if (CurrentSkin == null || string.IsNullOrEmpty(CurrentSkin.Path)) return;

            string jsonPath = Path.Combine(CurrentSkin.Path, $"{CurrentSkin.Codename}_Settings.json");
            string jsonString = System.Text.Json.JsonSerializer.Serialize(CurrentSkin, SaveOptions);

            File.WriteAllText(jsonPath, jsonString);
        }

        public void SaveSeries(IObservableVector<object> sender, IVectorChangedEventArgs e)
        {
            AppSettings.SetValue("AvailableSeries", seriesComboBox.Items);
        }
    }

    public class SkinData : INotifyPropertyChanged
    {
        [JsonIgnore]
        public string Codename { get; set; } = string.Empty;
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }

        }
        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }

        }
        private string _rarity = "Common";
        public string Rarity
        {
            get => _rarity;
            set
            {
                if (_rarity != value)
                {
                    _rarity = value;
                    OnPropertyChanged();
                }
            }

        }
        private string _series = "None";
        public string Series
        {
            get => _series;
            set
            {
                if (_series != value)
                {
                    _series = value;
                    OnPropertyChanged();
                }
            }

        }
        private string _gender;
        public string Gender
        {
            get => _gender;
            set
            {
                if (_gender != value)
                {
                    _gender = value;
                    OnPropertyChanged();
                }
            }

        }
        private string _smallIcon = string.Empty;
        public string SmallIcon 
        {
            get => _smallIcon;
            set
            {
                if (_smallIcon != value)
                {
                    _smallIcon = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _largeIcon = string.Empty;
        public string LargeIcon
        {
            get => _largeIcon;
            set
            {
                if (_largeIcon != value)
                {
                    _largeIcon = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _cid = string.Empty;
        public string CID
        {
            get => _cid;
            set
            {
                if (_cid != value)
                {
                    _cid = value;
                    OnPropertyChanged();
                }
            }

        }
        [JsonIgnore]
        public string LobbyAnimationPsa { get; set; } = string.Empty;
        [JsonIgnore]
        public string LobbyAnimationJson { get; set; } = string.Empty;
        [JsonIgnore]
        public string LobbyAnimationFbx { get; set; } = string.Empty;
        [JsonIgnore]
        public string OutputContentPath { get; set; } = string.Empty;
        public ObservableCollection<Material> Materials { get; set; } = new();
        [JsonIgnore]
        public string Path = string.Empty;
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;
        [JsonIgnore]
        public string MeshesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string TexturesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string PhysicsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string LobbyAnimationFolderPath { get; set; } = string.Empty;
        [JsonIgnore]
        public float LobbyAnimationLength { get; set; } = 0;
        [JsonIgnore]
        public List<CharacterPart> CharacterParts { get; set; } = new();
        [JsonIgnore]
        public List<string> Textures { get; set; } = new();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public SkinData Clone()
        {
            SkinData clone = (SkinData)this.MemberwiseClone();
            clone.Materials = new ObservableCollection<Material>(Materials.Select(m => m.Clone()));
            clone.CharacterParts = CharacterParts.Select(cp => cp.Clone()).ToList();
            return clone;
        }
    }

    public class Material : INotifyPropertyChanged
    {
        [JsonIgnore]
        public bool isLoading = true;
        [JsonIgnore]
        public Windows.Globalization.NumberFormatting.DecimalFormatter DotFormatter { get; } =
            new Windows.Globalization.NumberFormatting.DecimalFormatter(new[] { "en-US" }, "US");
        public string Name { get; set; }
        [JsonIgnore]
        public List<string> TextureOptions { get; set; }
        private string _selectedDiffuse = "Default_Diffuse";
        public string SelectedDiffuse
        {
            get => _selectedDiffuse;
            set
            {
                if (_selectedDiffuse != value)
                {
                    _selectedDiffuse = value;
                    JsonSelectedDiffuse = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrEmpty(value) && value.Length > 0)
                    {
                        string baseName = value.Substring(0, value.Length - 1);
                        string mask = baseName + "M";
                        string normal = baseName + "N";
                        string spec = baseName + "S";

                        if (TextureOptions?.Contains(mask) == true) SelectedMask = mask;
                        if (TextureOptions?.Contains(normal) == true) SelectedNormal = normal;
                        if (TextureOptions?.Contains(spec) == true) SelectedSpecular = spec;
                    }
                }
            }
        }
        public string JsonSelectedDiffuse { get; set; }

        private string _selectedMask = "Default_Mask";
        public string SelectedMask
        {
            get => _selectedMask;
            set
            {
                if (_selectedMask != value)
                {
                    _selectedMask = value;
                    JsonSelectedMask = value;
                    OnPropertyChanged();
                }
            }
        }
        public string JsonSelectedMask { get; set; }

        private string _selectedNormal = "Default_Normal";
        public string SelectedNormal
        {
            get => _selectedNormal;
            set
            {
                if (_selectedNormal != value)
                {
                    _selectedNormal = value;
                    JsonSelectedNormal = value;
                    OnPropertyChanged();
                }
            }
        }
        public string JsonSelectedNormal { get; set; }

        private string _selectedSpecular = "Default_Specular";
        public string SelectedSpecular
        {
            get => _selectedSpecular;
            set
            {
                if (_selectedSpecular != value)
                {
                    _selectedSpecular = value;
                    JsonSelectedSpecular = value;
                    OnPropertyChanged();
                }
            }
        }
        public string JsonSelectedSpecular { get; set; }

        private bool _useSkinBoostColor = false;
        public bool UseSkinBoostColor
        {
            get => _useSkinBoostColor;

            set
            {
                if (_useSkinBoostColor != value)
                {
                    _useSkinBoostColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _sbcRed = 0f;
        public float SbcRed
        {
            get => _sbcRed;

            set
            {
                if (float.IsNaN(value)) value = 0f;
                _sbcRed = value;

                if (ParentPage?.CurrentSkin?.Materials == null)
                {
                    OnPropertyChanged();
                    return;
                }

                foreach (Material mat in ParentPage?.CurrentSkin.Materials)
                {
                    if (!mat.UseSkinBoostColor && Math.Abs(mat._sbcRed - value) > 0.0001f)
                    {
                        mat.SbcRed = value;
                    }
                }

                if (UseSkinBoostColor) Console.WriteLine($"Changed Skin Boost Color And Exponent's red to {value} on {Name}");
                OnPropertyChanged();
            }
        }

        private float _sbcGreen = 0f;
        public float SbcGreen
        {
            get => _sbcGreen;

            set
            {
                if (float.IsNaN(value)) value = 0f;
                _sbcGreen = value;

                if (ParentPage?.CurrentSkin?.Materials == null)
                {
                    OnPropertyChanged();
                    return;
                }

                foreach (Material mat in ParentPage?.CurrentSkin.Materials)
                {
                    if (!mat.UseSkinBoostColor && Math.Abs(mat._sbcGreen - value) > 0.0001f)
                    {
                        mat.SbcGreen = value;
                    }
                }

                if (UseSkinBoostColor) Console.WriteLine($"Changed Skin Boost Color And Exponent's green to {value} on {Name}");
                OnPropertyChanged();
            }
        }

        private float _sbcBlue = 0f;
        public float SbcBlue
        {
            get => _sbcBlue;

            set
            {
                if (float.IsNaN(value)) value = 0f;
                _sbcBlue = value;

                if (ParentPage?.CurrentSkin?.Materials == null)
                {
                    OnPropertyChanged();
                    return;
                }

                foreach (Material mat in ParentPage?.CurrentSkin.Materials)
                {
                    if (!mat.UseSkinBoostColor && Math.Abs(mat._sbcBlue - value) > 0.0001f)
                    {
                        mat.SbcBlue = value;
                    }
                }

                if (UseSkinBoostColor) Console.WriteLine($"Changed Skin Boost Color And Exponent's blue to {value} on {Name}");
                OnPropertyChanged();
            }
        }

        private float _sbcAlpha = 0f;
        public float SbcAlpha
        {
            get => _sbcAlpha;

            set
            {
                if (float.IsNaN(value)) value = 0f;
                _sbcAlpha = value;

                if (ParentPage?.CurrentSkin?.Materials == null)
                {
                    OnPropertyChanged();
                    return;
                }

                foreach (Material mat in ParentPage?.CurrentSkin.Materials)
                {
                    if (!mat.UseSkinBoostColor && Math.Abs(mat._sbcAlpha - value) > 0.0001f)
                    {
                        mat.SbcAlpha = value;
                    }
                }

                if (UseSkinBoostColor) Console.WriteLine($"Changed Skin Boost Color And Exponent's alpha to {value} on {Name}");
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public SkinsPage ParentPage { get; set; }

        private bool _swizzle = false;
        public bool Swizzle
        {
            get => _swizzle;
            set
            {
                if (_swizzle != value)
                {
                    _swizzle = value;
                    OnPropertyChanged();
                    ParentPage?.UpdateAllSwizzleCheckBoxState();
                }
            }
        }

        [JsonIgnore]
        public CharacterPart Cp { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (!isLoading) ParentPage?.SaveSkinConfig();
        }

        public Material Clone()
        {
            return (Material)this.MemberwiseClone();
        }
    }

    [JsonSerializable(typeof(BlenderExportData))]
    [JsonSerializable(typeof(UnrealExportSkinData))]
    [JsonSerializable(typeof(UnrealExportEmoteData))]
    internal partial class AppJsonContext : JsonSerializerContext { }
}