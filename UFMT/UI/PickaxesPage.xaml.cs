using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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

            CurrentPickaxe = new PickaxeData();
            if (!PickaxeValidator.ValidateAfterPathChange((sender as TextBox)?.Text, CurrentPickaxe)) return;

            try
            {
                (bool success, CurrentPickaxe.PskPath, CurrentPickaxe.PhysicsAssetJsons) = PickaxeFolderScanner.GetMeshData(CurrentPickaxe.MeshesPath, CurrentPickaxe.PhysicsPath);
                if (!success) return;
                (success, CurrentPickaxe.ImpactSoundWaves, CurrentPickaxe.ReadySoundWaves, CurrentPickaxe.SwingSoundWaves) = PickaxeFolderScanner.FindSoundFiles(CurrentPickaxe.SoundsPath);
                if (!success) return;


                (string largeIcon, string smallIcon) = TextureCategorizer.GetIconTextures(CurrentPickaxe.TexturesPath, "pickaxe");
                if (largeIcon == null || smallIcon == null) return;
                if (largeIcon != string.Empty) CurrentPickaxe.LargeIcon = $"{largeIcon}.png";
                if (smallIcon != string.Empty) CurrentPickaxe.SmallIcon = $"{smallIcon}.png";

                CurrentPickaxe.PID = $"Pickaxe_{CurrentPickaxe.Codename}";
                CurrentPickaxe.OutputContentPath = Path.Combine(CurrentPickaxe.Path, "Output", "FortniteGame", "Content");

                LoadPickaxeConfigInto(Path.Combine(CurrentPickaxe.Path, $"{CurrentPickaxe.Codename}_Settings.json"), CurrentPickaxe);
                Log.Test($"Current pickaxe name is {CurrentPickaxe.Name}, Name textbox is displaying {characterNameTextBox.Text}");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while changing current pickaxe path: {ex.Message}");
                return;
            }

            CurrentPickaxe.PropertyChanged += (s, e) => SavePickaxeConfig();
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

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {

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

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sound", "Impact"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sound", "Ready"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Sound", "Swing"));

            Directory.CreateDirectory(Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text, "Source", "Textures"));

            Log.Success($"Successfully created pickaxe folder {CodenameFolderCreateTextBox.Text} at {PickaxesPathTextBox.Text}");
            System.Diagnostics.Process.Start("explorer.exe", Path.Combine(PickaxesPathTextBox.Text, CodenameFolderCreateTextBox.Text));

            args.Cancel = false;
        }

        private void AddSeriesDialog_SecondaryButtonClick (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void AddSeriesDialogClosed(object sender, ContentDialogClosedEventArgs e)
        {

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
        }

        public void SavePickaxeConfig()
        {
            if (CurrentPickaxe == null || string.IsNullOrEmpty(CurrentPickaxe.Path)) return;

            string jsonPath = Path.Combine(CurrentPickaxe.Path, $"{CurrentPickaxe.Codename}_Settings.json");
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string jsonString = System.Text.Json.JsonSerializer.Serialize(CurrentPickaxe, options);

            File.WriteAllText(jsonPath, jsonString);
        }
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
        public string MeshesPath { get; set; } = string.Empty;
        public string PhysicsPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string TexturesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string SoundsPath { get; set; } = string.Empty;
        public string PskPath { get; set; } = string.Empty;
        public string[] PhysicsAssetJsons { get; set; }
        [JsonIgnore]
        internal string[] ImpactSoundWaves { get; set; }
        [JsonIgnore]
        internal string[] ReadySoundWaves { get; set; }
        [JsonIgnore]
        internal string[] SwingSoundWaves { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public PickaxeData Clone()
        {
            PickaxeData clone = (PickaxeData)this.MemberwiseClone();
            return clone;
        }
    }
}
