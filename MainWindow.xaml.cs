using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SteamManifestToggler
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<GameEntry> _allGames = new();
        private readonly ICollectionView _view;
        private readonly AppConfigData _config;
        private readonly ObservableCollection<string> _libraryOptions = new();
        private string? _steamRoot;
        private const string AllLibrariesOption = "All Libraries";

        public MainWindow(AppConfigData? config)
        {
            InitializeComponent();
            _config = config ?? new AppConfigData();;
            _view = CollectionViewSource.GetDefaultView(_allGames);
            GridGames.ItemsSource = _view;
            LibraryFilter.ItemsSource = _libraryOptions;
            if (!_libraryOptions.Contains(AllLibrariesOption))
                _libraryOptions.Add(AllLibrariesOption);
            LibraryFilter.SelectedItem = AllLibrariesOption;
            StatusFilter.SelectedIndex = 0;

            // Prompt for root on startup
            if (!string.IsNullOrWhiteSpace(_config.SteamRoot))
            {
                ApplySteamRoot(_config.SteamRoot!);
            }
            else
            {
                StatusText.Text = "Select Steam root to begin…";
            }
        }

        private void PickRoot(string? initial = null)
        {
            using var dlg = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
                dlg.SelectedPath = initial;
            dlg.Description = "Select your Steam ROOT folder (must contain 'config' and a 'libraryfolders.vdf' under steamapps or config/).";
            var result = dlg.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                StatusText.Text = "No root selected. Use 'Select Root…'.";
                return;
            }

            var root = dlg.SelectedPath;
            if (!SteamScanner.IsValidSteamRoot(root))
            {
                System.Windows.MessageBox.Show(
                    "The selected directory does not look like a Steam root.\nIt must contain a 'config' folder and a 'libraryfolders.vdf' in steamapps/ or config/.",
                    "Invalid root", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ApplySteamRoot(root);
        }
        
        private void ApplySteamRoot(string root)
        {
            _steamRoot = root;
            RootBox.Text = _steamRoot;
            _config.SteamRoot = root;
            AppConfig.Save(_config);
            RefreshGames();
        }

        private void RefreshGames()
        {
            if (!SteamScanner.IsValidSteamRoot(_steamRoot))
            {
                _allGames.Clear();
                _view.Refresh();
                _libraryOptions.Clear();
                _libraryOptions.Add(AllLibrariesOption);
                LibraryFilter.SelectedItem = AllLibrariesOption;
                StatusText.Text = "Steam root missing or invalid. Use 'Select Root…'.";
                return;
            }
            try
            {
                StatusText.Text = "Scanning…";
                var games = SteamScanner.ScanAllGamesFromRoot(_steamRoot!);
                _allGames.Clear();
                foreach (var g in games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                    _allGames.Add(g);
                UpdateLibraryOptions(games);
                ApplyFilter();
                StatusText.Text = $"Found {_allGames.Count} game(s). Double‑click to toggle.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void ApplyFilter()
        {
            var q = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            var status = (StatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
            var librarySelection = LibraryFilter.SelectedItem as string;
            _view.Filter = item =>
            {
                if (item is not GameEntry g) return false;
                if (!string.IsNullOrEmpty(q))
                {
                    var name = (g.Name ?? string.Empty).ToLowerInvariant();
                    var appId = (g.AppId ?? string.Empty).ToLowerInvariant();
                    if (!name.Contains(q) && !appId.Contains(q)) return false;
                }

                if (status == "ReadOnly" && !g.IsReadOnly) return false;
                if (status == "ReadWrite" && g.IsReadOnly) return false;

                if (!string.IsNullOrWhiteSpace(librarySelection) && librarySelection != AllLibrariesOption)
                {
                    if (!string.Equals(g.LibraryName, librarySelection, StringComparison.OrdinalIgnoreCase)) return false;
                }

                return true;
            };
            _view.Refresh();
        }
        
        private void UpdateLibraryOptions(IEnumerable<GameEntry> games)
        {
            var previousSelection = LibraryFilter.SelectedItem as string;
            var libs = games
                .Select(g => g.LibraryName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _libraryOptions.Clear();
            _libraryOptions.Add(AllLibrariesOption);
            foreach (var lib in libs)
                _libraryOptions.Add(lib);

            if (!string.IsNullOrWhiteSpace(previousSelection))
            {
                var match = _libraryOptions.FirstOrDefault(l => string.Equals(l, previousSelection, StringComparison.OrdinalIgnoreCase));
                LibraryFilter.SelectedItem = match ?? AllLibrariesOption;
            }
            else
            {
                LibraryFilter.SelectedItem = AllLibrariesOption;
            }
        }

        private IEnumerable<GameEntry> SelectedGames()
        {
            return GridGames.SelectedItems.Cast<GameEntry>();
        }

        private void ToggleSelected()
        {
            foreach (var g in SelectedGames())
            {
                try
                {
                    SteamScanner.SetManifestReadOnly(g.ManifestPath, !g.IsReadOnly, backupIfMissing: true);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed for {g.Name}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshGames();
            ApplyFilter();
        }

        private void BtnPickRoot_Click(object sender, RoutedEventArgs e) => PickRoot(_steamRoot ?? SteamScanner.GetDefaultSteamPath());
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshGames();
        private void BtnRO_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in SelectedGames())
            {
                try { SteamScanner.SetManifestReadOnly(g.ManifestPath, true, backupIfMissing: true); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            RefreshGames(); ApplyFilter();
        }
        private void BtnRW_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in SelectedGames())
            {
                try { SteamScanner.SetManifestReadOnly(g.ManifestPath, false, backupIfMissing: true); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            RefreshGames(); ApplyFilter();
        }
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var g = SelectedGames().FirstOrDefault();
            if (g == null) return;
            try
            {
                var dir = Path.GetDirectoryName(g.ManifestPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Open folder failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GridGames_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => ToggleSelected();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            ApplyFilter();
        }
        private void LibraryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            ApplyFilter();
        }
        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            StatusFilter.SelectedIndex = 0;
            LibraryFilter.SelectedItem = AllLibrariesOption;
            ApplyFilter();
        }
    }
}
