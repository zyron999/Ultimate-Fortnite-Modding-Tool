using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using UFMT.Blender;
using UFMT.Core;
using UFMT.FnAssets;
using UFMT.FnAssetsLogic;
using UFMT.MaterialTextureAssignment;
using UFMT.UnrealEngine;
using Windows.Foundation.Collections;


namespace UFMT.UI
{
    public sealed partial class PickaxesPage : Page, INotifyPropertyChanged
    {

        private CancellationTokenSource _currentPickaxePathDebounce;
        public event PropertyChangedEventHandler PropertyChanged;
        public Windows.Globalization.NumberFormatting.DecimalFormatter DotFormatter { get; } =
        new Windows.Globalization.NumberFormatting.DecimalFormatter(new[] { "en-US" }, "US");
        private PickaxeData _currentPickaxe;
        private bool IsUpdatingFromCode = false;
        private bool IsLoadingDropdowns = false;
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

        public PickaxeData CurrentPickaxe
        {
            get => _currentPickaxe;
            set
            {
                if (_currentPickaxe != value)
                {
                    _currentPickaxe = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPickaxe)));
                }
            }
        }
        public PickaxesPage()
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
            seriesComboBox.Items.VectorChanged += SaveSeries;

            PickaxesPathTextBox.Text = AppSettings.GetValue("PickaxesPath", string.Empty);
            CurrentPickaxe = new PickaxeData();
            CurrentPickaxePathTextBox.Text = AppSettings.GetValue("CurrentPickaxePath", string.Empty);
            CurrentPickaxePathTextBox_TextChanged(CurrentPickaxePathTextBox, null);

            ((FrameworkElement)this.Content).Loaded += (s, e) =>
            {
            };
        }

        public static FnVersion CurrentFnVersion = FnVersionsData.FnVersions.GetValueOrDefault(App.Settings.FnVersion);
        public static UeVersion CurrentUeVersion = UeVersionsData.UeVersions.GetValueOrDefault(App.Settings.UeVersion);
        public static string PreviouslySelectedSeries = "None";

        private void PickaxesPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("PickaxesPath", (sender as TextBox)?.Text);
        }

        private async void CurrentPickaxePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentPickaxePathDebounce?.Cancel();
            _currentPickaxePathDebounce = new CancellationTokenSource();
            var token = _currentPickaxePathDebounce.Token;
            try
            {
                await Task.Delay(250, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            AppSettings.SetValue("CurrentPickaxePath", (sender as TextBox).Text);

            try
            {
                CurrentPickaxe = new PickaxeData();
                UpdateDropdowns();
                if (!PickaxeValidator.ValidateAfterPathChange((sender as TextBox)?.Text, CurrentPickaxe)) return;
                (bool success, CurrentPickaxe.PskPath, CurrentPickaxe.PhysicsAssetJsons) = PickaxeFolderScanner.GetMeshData(CurrentPickaxe.MeshesPath, CurrentPickaxe.PhysicsPath);
                if (!success) return;
                DefaultTextureSetup.CreateDefaultTextures(DefaultTextureSetup.FindMissingDefaultTextures(CurrentPickaxe.TexturesPath), CurrentPickaxe.TexturesPath);
                CurrentPickaxe.Textures = Directory.GetFiles(CurrentPickaxe.TexturesPath).Select(tex => Path.GetFileNameWithoutExtension(tex)).ToList();
                CurrentPickaxe.Materials = new ObservableCollection<PickaxeMaterial>(PskReader.GetPickaxeMaterialData(CurrentPickaxe.PskPath, false, this, CurrentPickaxe.Textures));

                (success, CurrentPickaxe.ImpactSoundWaves, CurrentPickaxe.ReadySoundWaves, CurrentPickaxe.SwingSoundWaves) = PickaxeFolderScanner.FindSoundFiles(CurrentPickaxe.SoundsPath);
                if (!success) return;


                (string largeIcon, string smallIcon) = TextureCategorizer.GetIconTextures(CurrentPickaxe.TexturesPath, "pickaxe");
                if (largeIcon == null || smallIcon == null) return;
                if (largeIcon != string.Empty) CurrentPickaxe.LargeIcon = largeIcon;
                if (smallIcon != string.Empty) CurrentPickaxe.SmallIcon = smallIcon;
                MaterialTextureAssigner.AssignTexturesToAllPickaxeMaterials(CurrentPickaxe.Textures, CurrentPickaxe.Materials);

                CurrentPickaxe.PID = $"Pickaxe_{CurrentPickaxe.Codename}";
                CurrentPickaxe.OutputContentPath = Path.Combine(CurrentPickaxe.Path, "Output", "FortniteGame", "Content");

                LoadPickaxeConfigInto(Path.Combine(CurrentPickaxe.Path, $"{CurrentPickaxe.Codename}_Settings.json"), CurrentPickaxe);
                UpdateDropdowns();
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while changing current pickaxe path: {ex.Message}");
                return;
            }

            CurrentPickaxe.PropertyChanged += (s, e) => SavePickaxeConfig();
            foreach (PickaxeMaterial mat in CurrentPickaxe.Materials) mat.isLoading = false;
        }

        private void AllSwizzleChecked(object sender, RoutedEventArgs e)
        {
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;
            foreach (PickaxeMaterial mat in CurrentPickaxe.Materials)
            {
                mat.Swizzle = true;
            }
            IsUpdatingFromCode = false;
        }

        private void AllSwizzleUnchecked(object sender, RoutedEventArgs e)
        {
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;
            foreach (PickaxeMaterial mat in CurrentPickaxe.Materials)
            {
                mat.Swizzle = false;
            }
            IsUpdatingFromCode = false;
        }

        public void UpdateAllSwizzleCheckBoxState()
        {
            if (CurrentPickaxe.Materials == null || CurrentPickaxe.Materials.Count == 0) return;
            if (IsUpdatingFromCode) return;

            IsUpdatingFromCode = true;

            bool allChecked = CurrentPickaxe.Materials.All(m => m.Swizzle);
            AllSwizzleCheckBoxValue = allChecked;

            IsUpdatingFromCode = false;
        }
        private void SmallIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentPickaxe == null || CurrentPickaxe.SmallIcon == null || (sender as ComboBox)?.Items == null) return;
            CurrentPickaxe.SmallIcon = (sender as ComboBox).SelectedItem?.ToString();
            Console.WriteLine($"Changed the small icon to {CurrentPickaxe.SmallIcon}");
        }
        private void LargeIcon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentPickaxe == null || CurrentPickaxe.LargeIcon == null || (sender as ComboBox)?.Items == null) return;
            CurrentPickaxe.LargeIcon = (sender as ComboBox).SelectedItem?.ToString();
            Console.WriteLine($"Changed the large icon to {CurrentPickaxe.LargeIcon}");
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
                    if (button.Name == "PickaxesPathBrowse")
                    {
                        PickaxesPathTextBox.Text = folder.Path;
                    }
                    else if (button.Name == "CurrentPickaxePathBrowse")
                    {
                        CurrentPickaxePathTextBox.Text = folder.Path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private async void CreatePickaxeFolder_Click(object sender, RoutedEventArgs e)
        {
            CreateFolderDialog.XamlRoot = this.Content.XamlRoot;

            if (PickaxesPathTextBox.Text == null || PickaxesPathTextBox.Text == "")
            {
                Log.Error("The skins path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(PickaxesPathTextBox.Text))
            {
                Log.Error($"\"{PickaxesPathTextBox.Text}\" doesn't exist!");
                return;
            }

            CodenameFolderCreateTextBox.Text = "";
            var result = await CreateFolderDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newName = CodenameFolderCreateTextBox.Text;
                string rootPath = PickaxesPathTextBox.Text;
            }
        }

        private void Reimport_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPickaxe == null || CurrentPickaxe.Path == null || !Directory.Exists(CurrentPickaxe.Path) || CurrentPickaxe.Codename == null) return;

            string skinSettingsFilePath = Path.Combine(CurrentPickaxe.Path, $"{CurrentPickaxe.Codename}_Settings.json");
            if (File.Exists(skinSettingsFilePath)) File.Delete(skinSettingsFilePath);
            CurrentPickaxePathTextBox_TextChanged(CurrentPickaxePathTextBox, null);
            Log.Success($"Reimported {CurrentPickaxe.Codename}!");
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            PickaxeData exportPickaxe = CurrentPickaxe.Clone();
            UeVersion currentUeVersion = CurrentUeVersion;
            FnVersion currentFnVersion = CurrentFnVersion;
            string ueProjectPath = App.Settings.UeProjectPath;
            string ueExecutablePath = App.Settings.UeExecutablePath;
            string uePickaxesPackagePath = App.Settings.UePickaxesPackagePath;
            bool success = true;

            if (!PickaxeValidator.ValidateBeforeExportProcess(uePickaxesPackagePath, ueProjectPath, ueExecutablePath, exportPickaxe.Name, exportPickaxe.Description,
            exportPickaxe.Rarity, rarityComboBox.Items.Select(item => item as string).ToArray(), currentUeVersion, CurrentFnVersion)) return;
            if (currentFnVersion.ManuallySwizzleMaterials) success = 
            (TextureSwizzler.SwizzleSpecularTextures(exportPickaxe.TexturesPath, exportPickaxe.Materials.Select(mat => mat.SelectedSpecular).ToArray()));
            if (!success) return;

            exportPickaxe.FbxPath = Path.Combine(exportPickaxe.SourcePath, "Fbx", "Meshes", $"SK_{exportPickaxe.Codename}.fbx");
            if (!await FbxConverter.ConvertPskToFbx(exportPickaxe.PskPath, exportPickaxe.FbxPath)) return;

            string cookedAssetsPath = Path.Combine(Path.GetDirectoryName(ueProjectPath),
            "Saved", "Cooked", "WindowsNoEditor", Path.GetFileNameWithoutExtension(ueProjectPath), "Content"); ;
            string outputFnGamePath = Path.Combine(exportPickaxe.Path, "Output", currentFnVersion.Name, "FortniteGame");
            string uePickaxesOsPath = uePickaxesPackagePath.Substring(6, uePickaxesPackagePath.Length - 6).Replace("/", "\\"); //Remove /Game/ at the start and replace / with \
            string pluginPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Plugins", "PhysicsImporter");
            string physicsImporterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"PhysicsImporter_{currentUeVersion.Name}.zip");
            string cookedExportEmotePath = Path.Combine(cookedAssetsPath, uePickaxesOsPath, exportPickaxe.Codename);
            string OutputFnGameExportEmoteFolder = Path.Combine(outputFnGamePath, "Content", uePickaxesOsPath, exportPickaxe.Codename);

            UnrealExportPickaxeData unrealData = UnrealExportDataCollector.CollectPickaxeData(exportPickaxe, uePickaxesPackagePath, currentUeVersion.Name);
            if (unrealData == null) return;

            if (!await UnrealDependencySetup.AddRequiredUeAssetsBeforeExport(ueProjectPath, currentUeVersion.BaseHeadPath, cookedExportEmotePath, currentUeVersion.Name,
            currentUeVersion.BaseHeadFileNames, pluginPath, physicsImporterPath)) return;

            string jsonString = JsonSerializer.Serialize(unrealData, AppJsonContext.Default.UnrealExportPickaxeData);
            await UnrealProcessRunner.LaunchUnreal(jsonString, ueProjectPath, ueExecutablePath, "pickaxe");
            if (!PickaxeValidator.ValidateAfterUeProcess(ueProjectPath, uePickaxesOsPath, exportPickaxe.Codename, unrealData.DiffuseTexturePaths, unrealData.MaskTexturePaths,
            unrealData.NormalTexturePaths, unrealData.SpecularTexturePaths, unrealData.MaterialNames.ToList(), Path.GetFileNameWithoutExtension(exportPickaxe.FbxPath),
            exportPickaxe.SmallIcon, exportPickaxe.LargeIcon, unrealData.SoundWavPaths, exportPickaxe.PID, cookedAssetsPath))
            {
                Log.Error($"Unreal Engine import process failed!");
                return;
            }

            await UnrealProcessRunner.CookFiles(ueProjectPath, ueExecutablePath);

            if (!PickaxeValidator.ValidateAfterUeProcess(ueProjectPath, uePickaxesOsPath, exportPickaxe.Codename, unrealData.DiffuseTexturePaths, unrealData.MaskTexturePaths,
            unrealData.NormalTexturePaths, unrealData.SpecularTexturePaths, unrealData.MaterialNames.ToList(), Path.GetFileNameWithoutExtension(exportPickaxe.FbxPath),
            exportPickaxe.SmallIcon, exportPickaxe.LargeIcon, unrealData.SoundWavPaths, exportPickaxe.PID, cookedAssetsPath, true))
            {
                Log.Error($"Unreal Engine cook process failed!");
                return;
            }
        }

        private void UpdateDropdowns()
        {
            try
            {
                IsLoadingDropdowns = true;

                foreach (PickaxeMaterial mat in CurrentPickaxe.Materials)
                {
                    mat.TextureOptions = CurrentPickaxe.Textures;
                    if (!mat.TextureOptions.Contains(mat.SelectedDiffuse))
                    {
                        Log.Error($"Diffuse texture '{mat.SelectedDiffuse}' on material '{mat.Name}' saved in pickaxe's settings does not exist");
                        mat.SelectedDiffuse = "Default_Diffuse";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedMask))
                    {
                        Log.Error($"Mask texture '{mat.SelectedMask}' on material '{mat.Name}' saved in pickaxe's settings does not exist");
                        mat.SelectedMask = "Default_Mask";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedNormal))
                    {
                        Log.Error($"Normal texture '{mat.SelectedNormal}' on material '{mat.Name}' saved in pickaxe's settings does not exist");
                        mat.SelectedNormal = "Default_Normal";
                    }
                    if (!mat.TextureOptions.Contains(mat.SelectedSpecular))
                    {
                        Log.Error($"Specular texture '{mat.SelectedSpecular}' on material '{mat.Name}' saved in pickaxe's settings does not exist");
                        mat.SelectedSpecular = "Default_Specular";
                    }
                }

                DynamicExpanderList.ItemsSource = CurrentPickaxe.Materials;

                void OnLayoutUpdated(object s, object e)
                {
                    DynamicExpanderList.LayoutUpdated -= OnLayoutUpdated;
                    IsLoadingDropdowns = false;
                    
                    if (!CurrentPickaxe.Materials.Any(mat => !mat.Swizzle) && CurrentPickaxe.Materials.Count > 0) AllSwizzleCheckBoxValue = true;
                    else { IsUpdatingFromCode = true; AllSwizzleCheckBoxValue = false; IsUpdatingFromCode = false; }
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
            foreach (string texture in CurrentPickaxe.Textures)
            {
                SmallIconComboBox.Items.Add(texture);
                LargeIconComboBox.Items.Add(texture);
            }
            SmallIconComboBox.SelectedItem = CurrentPickaxe.SmallIcon;
            LargeIconComboBox.SelectedItem = CurrentPickaxe.LargeIcon;
        }

        private void CreateFolderDialog_SecondaryButtonClick
        (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (PickaxesPathTextBox.Text == null || PickaxesPathTextBox.Text == "")
            {
                Log.Error("The pickaxes path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(PickaxesPathTextBox.Text))
            {
                Log.Error($"\"{PickaxesPathTextBox.Text}\" doesn't exist!");
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
                Log.Error($"{CodenameFolderCreateTextBox.Text} contains invalid characters; only basic English letters (A-Z), numbers, and underscores are allowed.");
                return;
            }

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Meshes"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Physics"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sounds", "Impact"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sounds", "Ready"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sounds", "Swing"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Textures"));

            Log.Success($"Successfully created pickaxe folder {CodenameFolderCreateTextBox.Text} at {PickaxesPathTextBox.Text}");
            System.Diagnostics.Process.Start("explorer.exe", Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text));

            args.Cancel = false;
        }

        private void AddSeriesDialog_SecondaryButtonClick (ContentDialog sender, ContentDialogButtonClickEventArgs args)
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

        private void SaveSeries(IObservableVector<object> sender, IVectorChangedEventArgs e)
        {
            AppSettings.SetValue("AvailableSeries", seriesComboBox.Items);
        }

        private async void ComboBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox c = sender as ComboBox;

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

            if (CurrentPickaxe == null) return;
            if (c.Tag != null)
            {
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
            }
        }
        public void SavePickaxeConfig()
        {
            if (CurrentPickaxe == null || string.IsNullOrEmpty(CurrentPickaxe.Path)) return;

            string jsonPath = Path.Combine(CurrentPickaxe.Path, $"{CurrentPickaxe.Codename}_Settings.json");
            string jsonString = JsonSerializer.Serialize(CurrentPickaxe, SaveOptions);

            File.WriteAllText(jsonPath, jsonString);
        }
        private void LoadPickaxeConfigInto(string jsonPath, PickaxeData target)
        {
            if (!File.Exists(jsonPath)) return;

            string jsonString = File.ReadAllText(jsonPath);
            var node = System.Text.Json.Nodes.JsonNode.Parse(jsonString)?.AsObject();
            if (node == null) return;

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
                    Console.WriteLine($"Detected new series on the loaded pickaxe, added \"{currentSeries}\"");
                }
            }

            var loadedPickaxe = System.Text.Json.JsonSerializer.Deserialize<PickaxeData>(node.ToJsonString());
            if (loadedPickaxe == null) return;

            foreach (var prop in typeof(PickaxeData).GetProperties())
            {
                if (!prop.CanWrite || prop.IsDefined(typeof(JsonIgnoreAttribute), false))
                    continue;

                var val = prop.GetValue(loadedPickaxe);
                prop.SetValue(target, val);
            }

            try
            {
                if (target.Materials != null && loadedPickaxe.Materials != null)
                {
                    foreach (var existingMat in target.Materials)
                    {
                        existingMat.ParentPage = this;
                        var jsonMat = loadedPickaxe.Materials.FirstOrDefault(mat => mat?.Name == existingMat.Name);
                        if (jsonMat != null)
                        {
                            existingMat.Swizzle = jsonMat.Swizzle;

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
    }

    public class PickaxeData : INotifyPropertyChanged
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
        private string _smallIcon = string.Empty;
        [JsonIgnore]
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
        [JsonIgnore]
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
        private string _pid = string.Empty;
        public string PID
        {
            get => _pid;
            set
            {
                if (_pid != value)
                {
                    _pid = value;
                    OnPropertyChanged();
                }
            }

        }
        [JsonIgnore]
        public string OutputContentPath { get; set; } = string.Empty;

        public string Path = string.Empty;
        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;
        [JsonIgnore]
        public string MeshesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string PhysicsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string TexturesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string SoundsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string PskPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string FbxPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string[] PhysicsAssetJsons { get; set; }
        [JsonIgnore]
        internal string[] ImpactSoundWaves { get; set; }
        [JsonIgnore]
        internal string[] ReadySoundWaves { get; set; }
        [JsonIgnore]
        internal string[] SwingSoundWaves { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PickaxeMaterial> Materials { get; set; } = new();
        [JsonIgnore]
        public List<string> Textures { get; set; } = new();
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public PickaxeData Clone()
        {
            PickaxeData clone = (PickaxeData)this.MemberwiseClone();
            clone.Materials = new ObservableCollection<PickaxeMaterial>(Materials.Select(m => m.Clone()));
            return clone;
        }
    }

    public class PickaxeMaterial : INotifyPropertyChanged
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
        [JsonIgnore]
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
        [JsonIgnore]
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
        [JsonIgnore]
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
        [JsonIgnore]
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
        [JsonIgnore]
        public PickaxesPage ParentPage { get; set; }
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (!isLoading) ParentPage?.SavePickaxeConfig();
        }

        public PickaxeMaterial Clone()
        {
            return (PickaxeMaterial)this.MemberwiseClone();
        }
    }
}
