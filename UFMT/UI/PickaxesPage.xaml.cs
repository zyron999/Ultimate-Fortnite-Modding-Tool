using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using UFMT.Core;


namespace UFMT.UI
{
    public sealed partial class PickaxesPage : Page
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

            PickaxesPathTextBox.Text = AppSettings.GetValue("PickaxesPath", string.Empty);
            CurrentPickaxe = new PickaxeData();
            CurrentPickaxePathTextBox.Text = AppSettings.GetValue("CurrentPickaxePath", string.Empty);
            CurrentPickaxePathTextBox_TextChanged(CurrentPickaxePathTextBox, null);

            ((FrameworkElement)this.Content).Loaded += (s, e) =>
            {
            };
        }

        private void PickaxesPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("PickaxesPath", (sender as TextBox)?.Text);
        }

        private void CurrentPickaxePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.SetValue("CurrentPickaxePath", (sender as TextBox)?.Text);
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
        public string TexturesPath { get; set; } = string.Empty;
        [JsonIgnore]
        public string SoundsPath { get; set; } = string.Empty;
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
        public EmoteData Clone()
        {
            EmoteData clone = (EmoteData)this.MemberwiseClone();
            return clone;
        }
    }
}
