using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace SteamManifestToggler
{
    public partial class StartupWindow : Window
    {
        public string? SelectedRoot { get; private set; }

        public StartupWindow(string? initialRoot)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(initialRoot))
            {
                RootTextBox.Text = initialRoot;
                SelectedRoot = initialRoot;
            }

            UpdateState();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(RootTextBox.Text) && Directory.Exists(RootTextBox.Text))
                dlg.SelectedPath = RootTextBox.Text;
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                RootTextBox.Text = dlg.SelectedPath;
                SelectedRoot = dlg.SelectedPath;
                UpdateState();
            }
        }

        private void RootTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SelectedRoot = RootTextBox.Text?.Trim();
            UpdateState();
        }

        private void UpdateState()
        {
            var hasValue = !string.IsNullOrWhiteSpace(SelectedRoot);
            var isValid = hasValue && SteamScanner.IsValidSteamRoot(SelectedRoot);

            ContinueButton.IsEnabled = isValid;

            if (!hasValue)
            {
                ErrorText.Visibility = Visibility.Collapsed;
                return;
            }

            if (isValid)
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorText.Text = "That folder does not look like a Steam installation. Please choose a Steam Folder.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (!SteamScanner.IsValidSteamRoot(SelectedRoot))
            {
                UpdateState();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}