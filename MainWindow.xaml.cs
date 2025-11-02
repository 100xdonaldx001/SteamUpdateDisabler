using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;

namespace SteamManifestToggler
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<GameEntry> _allGames = new();
        private ICollectionView _view;
        private string? _steamRoot;

        public MainWindow()
        {
            InitializeComponent();
            _view = CollectionViewSource.GetDefaultView(_allGames);
            GridGames.ItemsSource = _view;

            // Prompt for root on startup
            var def = SteamScanner.GetDefaultSteamPath();
            PickRoot(def);
        }

        private void PickRoot(string? initial = null)
        {
            using var dlg = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
                dlg.SelectedPath = initial;
            dlg.Description = "Select your Steam ROOT folder (must contain 'config' and a 'libraryfolders.vdf' under steamapps/ or config/).";
            var result = dlg.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                StatusText.Text = "No root selected. Use 'Select Root…'.";
                return;
            }

            var root = dlg.SelectedPath;
            var vdf = SteamScanner.ResolveLibraryVdfPath(root);
            if (!Directory.Exists(Path.Combine(root, "config")) || string.IsNullOrEmpty(vdf))
            {
                System.Windows.MessageBox.Show(
                    "The selected directory does not look like a Steam root.\nIt must contain a 'config' folder and a 'libraryfolders.vdf' in steamapps/ or config/.",
                    "Invalid root", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _steamRoot = root;
            RootBox.Text = _steamRoot;
            RefreshGames();
        }

        private void RefreshGames()
        {
            if (string.IsNullOrWhiteSpace(_steamRoot)) return;
            try
            {
                StatusText.Text = "Scanning…";
                var games = SteamScanner.ScanAllGamesFromRoot(_steamRoot);
                _allGames.Clear();
                foreach (var g in games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                    _allGames.Add(g);
                _view.Refresh();
                StatusText.Text = $"[{_steamRoot}] — Found {_allGames.Count} game(s). Double‑click to toggle.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void ApplyFilter()
        {
            var q = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _view.Filter = item =>
            {
                if (item is not GameEntry g) return false;
                if (string.IsNullOrEmpty(q)) return true;
                return (g.Name ?? string.Empty).ToLower().Contains(q) || (g.AppId ?? string.Empty).ToLower().Contains(q);
            };
            _view.Refresh();
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
    }
}
