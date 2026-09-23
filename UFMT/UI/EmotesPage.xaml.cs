using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed partial class EmotesPage : Page, INotifyPropertyChanged
    {
        private CancellationTokenSource _currentEmotePathDebounce;
        public event PropertyChangedEventHandler PropertyChanged;
        public Windows.Globalization.NumberFormatting.DecimalFormatter DotFormatter { get; } =
        new Windows.Globalization.NumberFormatting.DecimalFormatter(new[] { "en-US" }, "US");
        private EmoteData _currentEmote;
        public EmoteData CurrentEmote
        {
            get => _currentEmote;
            set
            {
                if (_currentEmote != value)
                {
                    _currentEmote = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentEmote)));
                }
            }
        }
        public EmotesPage()
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

            EmotesPathTextBox.Text = AppSettings.GetValue("EmotesPath", string.Empty);
            CurrentEmote = new EmoteData();
            CurrentEmotePathTextBox.Text = AppSettings.GetValue("CurrentEmotePath", string.Empty);
            CurrentEmotePathTextBox_TextChanged(CurrentEmotePathTextBox, null);

            ((FrameworkElement)this.Content).Loaded += (s, e) =>
            {
            };
        }
        public static FnVersion CurrentFnVersion = FnVersionsData.FnVersions.GetValueOrDefault(App.Settings.FnVersion);
        public static UeVersion CurrentUeVersion = UeVersionsData.UeVersions.GetValueOrDefault(App.Settings.UeVersion);
        public static string PreviouslySelectedSeries = "None";


        private void EmotesPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("EmotesPath", (sender as TextBox)?.Text);
        }
        private async void CurrentEmotePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentEmotePathDebounce?.Cancel();
            _currentEmotePathDebounce = new CancellationTokenSource();
            var token = _currentEmotePathDebounce.Token;
            try
            {
                await Task.Delay(250, token);
            }
            catch (TaskCanceledException)
            {
                Log.Test("Returned because of task cancellation exception!");
                return;
            }
            AppSettings.SetValue("CurrentEmotePath", (sender as TextBox).Text);

            CurrentEmote = new EmoteData();
            if (!EmoteValidator.ValidateAfterPathChange((sender as TextBox)?.Text, CurrentEmote)) return;

            try
            {
                (bool success, string maleAnim, string femaleAnim) = EmoteFolderScanner.GetAnimationPsaData(CurrentEmote.AnimationsPath);
                if (!success) return;
                CurrentEmote.MaleAnimationPsa = $"{maleAnim}.psa";
                CurrentEmote.FemaleAnimationPsa = $"{femaleAnim}.psa";
                (CurrentEmote.MaleAnimationJson, CurrentEmote.FemaleAnimationJson) = EmoteFolderScanner.GetAnimationJsonData
                (CurrentEmote.MaleAnimationPsa, CurrentEmote.FemaleAnimationPsa, CurrentEmote.AnimationsPath);

                (success, string wav) = EmoteFolderScanner.GetSoundData(CurrentEmote.SoundPath);
                if (!success) return;
                CurrentEmote.SoundWav = wav;

                (string largeIcon, string smallIcon) = TextureCategorizer.GetIconTextures(CurrentEmote.IconsPath, "emote");
                if (largeIcon == null || smallIcon == null) return;
                if (largeIcon != string.Empty) CurrentEmote.LargeIcon = $"{largeIcon}.png";
                if (smallIcon != string.Empty) CurrentEmote.SmallIcon = $"{smallIcon}.png";

                CurrentEmote.MaleAnimationLength = Math.Round(PsaReader.GetAnimationLength(Path.Combine(CurrentEmote.AnimationsPath, CurrentEmote.MaleAnimationPsa)) / 30.0, 6);
                CurrentEmote.FemaleAnimationLength = Math.Round(PsaReader.GetAnimationLength(Path.Combine(CurrentEmote.AnimationsPath, CurrentEmote.FemaleAnimationPsa)) / 30.0, 6);

                CurrentEmote.EID = $"EID_{CurrentEmote.Codename}";
                CurrentEmote.OutputContentPath = Path.Combine(CurrentEmote.Path, "Output", "FortniteGame", "Content");

                LoadEmoteConfigInto(Path.Combine(CurrentEmote.Path, $"{CurrentEmote.Codename}_Settings.json"), CurrentEmote);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while changing current emote path: {ex.Message}");
                return;
            }

            CurrentEmote.PropertyChanged += (s, e) => SaveEmoteConfig();
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
                    if (button.Name == "EmotesPathBrowse")
                    {
                        EmotesPathTextBox.Text = folder.Path;
                    }
                    else if (button.Name == "CurrentEmotePathBrowse")
                    {
                        CurrentEmotePathTextBox.Text = folder.Path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }
        private async void CreateEmoteFolder_Click(object sender, RoutedEventArgs e) 
        {
            CreateFolderDialog.XamlRoot = this.Content.XamlRoot;

            if (EmotesPathTextBox.Text == null || EmotesPathTextBox.Text == "")
            {
                Log.Error("The skins path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(EmotesPathTextBox.Text))
            {
                Log.Error($"\"{EmotesPathTextBox.Text}\" doesn't exist!");
                return;
            }

            CodenameFolderCreateTextBox.Text = "";
            var result = await CreateFolderDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newName = CodenameFolderCreateTextBox.Text;
                string rootPath = EmotesPathTextBox.Text;
            }
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

            if (CurrentEmote == null) return;
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
        private void CreateFolderDialog_SecondaryButtonClick
        (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (EmotesPathTextBox.Text == null || EmotesPathTextBox.Text == "")
            {
                Log.Error("The emotes path cannot be empty!");
                return;
            }
            else if (!Directory.Exists(EmotesPathTextBox.Text))
            {
                Log.Error($"\"{EmotesPathTextBox.Text}\" doesn't exist!");
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

            Directory.CreateDirectory(Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text));
            Log.Success($"Successfully created {CodenameFolderCreateTextBox.Text} folder at {EmotesPathTextBox.Text}");

            Directory.CreateDirectory(Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Animations"));
            Log.Success($"Successfully created Animations folder at {Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source")}");

            Directory.CreateDirectory(Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sound"));
            Log.Success($"Successfully created Sound folder at {Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source")}");

            Directory.CreateDirectory(Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Icons"));
            Log.Success($"Successfully created Icons folder at {Path.Combine(EmotesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source")}");

            args.Cancel = false;
        }
        private void Reimport_Click(object sender, RoutedEventArgs e) 
        {
            if (CurrentEmote == null || CurrentEmote.Path == null || !Directory.Exists(CurrentEmote.Path) || CurrentEmote.Codename == null) 
            {
                Log.Test("Returned!");
                return;
            } 

            string skinSettingsFilePath = Path.Combine(CurrentEmote.Path, $"{CurrentEmote.Codename}_Settings.json");
            if (File.Exists(skinSettingsFilePath)) File.Delete(skinSettingsFilePath);
            CurrentEmotePathTextBox_TextChanged(CurrentEmotePathTextBox, null);
            Log.Success($"Reimported {CurrentEmote.Codename}!");

        }
        private void NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsNaN(args.NewValue) || double.IsInfinity(args.NewValue) || args.NewValue < 0)
            {
                sender.Value = 0;
            }
        }
        private void SaveSeries(IObservableVector<object> sender, IVectorChangedEventArgs e)
        {
            AppSettings.SetValue("AvailableSeries", seriesComboBox.Items);
        }
        public void SaveEmoteConfig()
        {
            if (CurrentEmote == null || string.IsNullOrEmpty(CurrentEmote.Path)) return;

            string jsonPath = Path.Combine(CurrentEmote.Path, $"{CurrentEmote.Codename}_Settings.json");
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string jsonString = System.Text.Json.JsonSerializer.Serialize(CurrentEmote, options);

            File.WriteAllText(jsonPath, jsonString);
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
        private void LoadEmoteConfigInto(string jsonPath, EmoteData target)
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
                    Console.WriteLine($"Detected new series on the loaded emote, added \"{currentSeries}\"");
                }
            }

            var loadedEmote = System.Text.Json.JsonSerializer.Deserialize<EmoteData>(node.ToJsonString());
            if (loadedEmote == null) return;

            foreach (var prop in typeof(EmoteData).GetProperties())
            {
                if (!prop.CanWrite || prop.IsDefined(typeof(JsonIgnoreAttribute), false))
                    continue;

                var val = prop.GetValue(loadedEmote);
                prop.SetValue(target, val);
            }
        }
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentEmote == null)
            {
                Log.Error("Current emote was null when trying to export!");
                return;
            }
            EmoteData exportEmote = CurrentEmote.Clone();
            string ueProjectPath = App.Settings.UeProjectPath;
            string ueExecutablePath = App.Settings.UeExecutablePath;
            string blenderPath = App.Settings.BlenderPath;
            string ueEmotesPackagePath = App.Settings.UeEmotesPackagePath;
            string ueSkinsPackagePath = App.Settings.UeSkinsPackagePath;
            UeVersion currentUeVersion = CurrentUeVersion;
            FnVersion currentFnVersion = CurrentFnVersion;

            if (!EmoteValidator.ValidateBeforeExportProcess(ueEmotesPackagePath, ueProjectPath, ueExecutablePath, exportEmote.Name, exportEmote.Description, exportEmote.Rarity,
            rarityComboBox.Items.Select(item => item as string).ToArray(), currentUeVersion, currentFnVersion)) return;

            string cookedAssetsPath = Path.Combine(Path.GetDirectoryName(ueProjectPath),
            "Saved", "Cooked", "WindowsNoEditor", Path.GetFileNameWithoutExtension(ueProjectPath), "Content"); ;
            string outputFnGamePath = Path.Combine(exportEmote.Path, "Output", CurrentFnVersion.Name);
            string ueEmotesOsPath = ueEmotesPackagePath.Substring(6, ueEmotesPackagePath.Length - 6).Replace("/", "\\"); //Remove /Game/ at the start and replace / with \
            string pluginPath = Path.Combine(Path.GetDirectoryName(ueProjectPath), "Plugins", "PhysicsImporter");
            string physicsImporterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"PhysicsImporter_{currentUeVersion.Name}.zip");

            exportEmote.MaleAnimationFbx = $"Emote_{exportEmote.Codename}_CMM.fbx";
            exportEmote.FemaleAnimationFbx = $"Emote_{exportEmote.Codename}_CMF.fbx";
            string cookedexportEmotePath = Path.Combine(cookedAssetsPath, ueEmotesOsPath, exportEmote.Codename);
            string OutputFnGameexportEmoteFolder = Path.Combine(outputFnGamePath, "Content", ueEmotesOsPath, exportEmote.Codename);

            if (!await FbxConverter.ConvertPsaToFbx(Path.Combine(exportEmote.SourcePath, "Animations", exportEmote.MaleAnimationPsa),
            Path.Combine(exportEmote.SourcePath, "Fbx", "Animations", exportEmote.MaleAnimationFbx), false)) return;

            if (exportEmote.MaleAnimationPsa == exportEmote.FemaleAnimationPsa)
            {
                // if male and female use the same animation, just copy the male one with female's name since it's faster than converting the same .psa file again
                File.Copy(Path.Combine(exportEmote.SourcePath, "Fbx", "Animations", exportEmote.MaleAnimationFbx),
                Path.Combine(exportEmote.SourcePath, "Fbx", "Animations", exportEmote.FemaleAnimationFbx));
                Log.Success($"Successfully converted {Path.GetFileName(exportEmote.FemaleAnimationPsa)} to {Path.GetFileName(exportEmote.FemaleAnimationFbx)}");
            }
            else
            {
                if (!await FbxConverter.ConvertPsaToFbx(Path.Combine(exportEmote.SourcePath, "Animations", exportEmote.FemaleAnimationPsa),
                Path.Combine(exportEmote.SourcePath, "Fbx", "Animations", exportEmote.FemaleAnimationFbx), false)) return;
            }

            UnrealExportEmoteData unrealData = UnrealExportDataCollector.CollectEmoteData(exportEmote, ueEmotesPackagePath, currentUeVersion.Name);
            if (unrealData == null) return;

            if (!await UnrealDependencySetup.AddRequiredUeAssetsBeforeExport(ueProjectPath, currentUeVersion.BaseHeadPath, cookedexportEmotePath, currentUeVersion.Name,
            currentUeVersion.BaseHeadFileNames, pluginPath, physicsImporterPath)) return;

            string jsonString = System.Text.Json.JsonSerializer.Serialize(unrealData, AppJsonContext.Default.UnrealExportEmoteData);

            await UnrealProcessRunner.LaunchUnreal(jsonString, ueProjectPath, ueExecutablePath, "emote");

            if (!EmoteValidator.ValidateAfterUeImport(ueProjectPath, ueEmotesOsPath, exportEmote.Codename, exportEmote.SmallIcon, exportEmote.LargeIcon, exportEmote.EID))
            {
                Log.Error($"Unreal Engine import process failed!");
                return;
            }

            await UnrealProcessRunner.CookFiles(ueProjectPath, ueExecutablePath);

            if (!EmoteValidator.ValidateAfterUeCook(cookedexportEmotePath, exportEmote.Codename, exportEmote.SmallIcon, exportEmote.LargeIcon, exportEmote.EID))
            {
                Log.Error($"Unreal Engine cook process failed!");
                return;
            }

            if (!Directory.Exists(cookedexportEmotePath))
            {
                Log.Error($"\"{cookedexportEmotePath}\" does not exist or is not a directory!");
                return;
            }

            currentUeVersion.FixRequiredFiles([Path.Combine(cookedexportEmotePath, "Animations", $"{Path.GetFileNameWithoutExtension(exportEmote.MaleAnimationFbx)}.uasset"),
            Path.Combine(cookedexportEmotePath, "Animations", $"{Path.GetFileNameWithoutExtension(exportEmote.FemaleAnimationFbx)}.uasset")], [string.Empty]);
            AssetRegistryBuilder.CreateAssetRegistry(cookedAssetsPath, currentUeVersion.Name, outputFnGamePath, ueSkinsPackagePath, ueEmotesPackagePath, exportEmote.Path);

            EmoteAssetCreator.CopyFilesFromUe(OutputFnGameexportEmoteFolder, new DirectoryInfo(cookedexportEmotePath));
            EmoteAssetCreator.CreateAnimationMontage(OutputFnGameexportEmoteFolder, Path.GetFileNameWithoutExtension(exportEmote.MaleAnimationFbx),
            (float)exportEmote.MaleAnimationLength, currentFnVersion, currentUeVersion, ueEmotesPackagePath, exportEmote.Codename, exportEmote.MaleAnimationJson,
            (float)exportEmote.LoopSectionStart);
            EmoteAssetCreator.CreateAnimationMontage(OutputFnGameexportEmoteFolder, Path.GetFileNameWithoutExtension(exportEmote.FemaleAnimationFbx),
            (float)exportEmote.FemaleAnimationLength, currentFnVersion, currentUeVersion, ueEmotesPackagePath, exportEmote.Codename, exportEmote.FemaleAnimationJson,
            (float)exportEmote.LoopSectionStart);
            EmoteAssetCreator.CreateSoundCues(OutputFnGameexportEmoteFolder, currentFnVersion, currentUeVersion, ueEmotesPackagePath, exportEmote.Codename);
            EmoteAssetCreator.CreateEid(outputFnGamePath, currentFnVersion, currentUeVersion, ueEmotesPackagePath, exportEmote.Codename,
            exportEmote.EID, exportEmote.Name, exportEmote.Description, exportEmote.Rarity, exportEmote.Series);
            U4Pak.Pack(outputFnGamePath, Path.Combine(Path.GetDirectoryName(outputFnGamePath), $"z_{exportEmote.Codename}.pak"));

            Log.Success("\nYour custom emote is ready! Check the output folder");
        }
        public static void PrintAllValues(EmoteData data)
        {
            foreach (var prop in typeof(EmoteData).GetProperties())
            {
                Console.WriteLine($"{prop.Name}: {prop.GetValue(data)}");
            }

            foreach (var field in typeof(EmoteData).GetFields())
            {
                // Ignores backing fields generated by properties
                if (!field.Name.EndsWith("k__BackingField"))
                {
                    Console.WriteLine($"{field.Name}: {field.GetValue(data)}");
                }
            }
        }
    }

    public class EmoteData : INotifyPropertyChanged
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
        private string _eid = string.Empty;
        public string EID
        {
            get => _eid;
            set
            {
                if (_eid != value)
                {
                    _eid = value;
                    OnPropertyChanged();
                }
            }

        }
        [JsonIgnore]
        public string MaleAnimationPsa { get; set; } = string.Empty;
        [JsonIgnore]
        public string MaleAnimationFbx { get; set; } = string.Empty;
        [JsonIgnore]
        public string MaleAnimationJson { get; set; } = string.Empty;
        private double _maleAnimationLength = 0;
        public double MaleAnimationLength
        {
            get => _maleAnimationLength;
            set
            {
                if (value != _maleAnimationLength)
                {
                    _maleAnimationLength = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _loopSectionStart = 0;
        public double LoopSectionStart
        {
            get => _loopSectionStart;
            set
            {
                if (value != _loopSectionStart)
                {
                    _loopSectionStart = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public string FemaleAnimationPsa { get; set; } = string.Empty;
        [JsonIgnore]
        public string FemaleAnimationFbx { get; set; } = string.Empty;
        [JsonIgnore]
        public string FemaleAnimationJson { get; set; } = string.Empty;
        private double _femaleAnimationLength = 0;
        public double FemaleAnimationLength
        {
            get => _femaleAnimationLength;
            set
            {
                if (value != _femaleAnimationLength)
                {
                    _femaleAnimationLength = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public string SoundWav { get; set; } = string.Empty;
        private int _soundWavCompressionQuality = 60;
        public int SoundWavCompressionQuality
        {
            get => _soundWavCompressionQuality;
            set
            {
                if (_soundWavCompressionQuality != value)
                {
                    _soundWavCompressionQuality = value;
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
        public string AnimationsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string IconsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string SoundPath { get; set; } = string.Empty;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public EmoteData Clone()
        {
            EmoteData clone = (EmoteData)this.MemberwiseClone();
            return clone;
        }
    }
}
