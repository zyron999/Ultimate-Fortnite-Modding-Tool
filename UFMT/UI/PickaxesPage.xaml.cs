using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using UFMT.Core;
using UFMT.FnAssetsLogic;
using UFMT.MaterialTextureAssignment;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UFMT.UI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PickaxesPage : Page
    {
        public PickaxesPage()
        {
            InitializeComponent();
        }

        private void PickaxesPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void CurrentPickaxePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

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
                        PickaxesPathTextBox.Text = folder.Path;
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

        private async void CurrentEmotePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            
        }

        private void Reimport_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CreateFolderDialog_SecondaryButtonClick(object sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void AddSeriesDialog_SecondaryButtonClick (ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void AddSeriesDialogClosed(object sender, ContentDialogClosedEventArgs e)
        {

        }
    }
}
