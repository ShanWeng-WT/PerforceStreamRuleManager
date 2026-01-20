using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;

namespace PerforceStreamManager.ViewModels
{
    /// <summary>
    /// View model for the history viewer window
    /// </summary>
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private readonly SnapshotService _snapshotService;
        private readonly SettingsService _settingsService;
        
        private Snapshot? _selectedSnapshot;
        private Snapshot? _comparisonSnapshot;
        private string? _streamPath;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Stream path for this history view
        /// </summary>
        public string? StreamPath
        {
            get => _streamPath;
            set
            {
                if (_streamPath != value)
                {
                    _streamPath = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Collection of snapshots for the current stream
        /// </summary>
        public ObservableCollection<Snapshot> Snapshots { get; }

        /// <summary>
        /// Currently selected snapshot in the timeline
        /// </summary>
        public Snapshot? SelectedSnapshot
        {
            get => _selectedSnapshot;
            set
            {
                if (_selectedSnapshot != value)
                {
                    _selectedSnapshot = value;
                    OnPropertyChanged();
                    
                    // Update diff results if comparison snapshot is set
                    if (_comparisonSnapshot != null && _selectedSnapshot != null)
                    {
                        UpdateDiffResults();
                    }
                }
            }
        }

        /// <summary>
        /// Snapshot to compare against the selected snapshot
        /// </summary>
        public Snapshot? ComparisonSnapshot
        {
            get => _comparisonSnapshot;
            set
            {
                if (_comparisonSnapshot != value)
                {
                    _comparisonSnapshot = value;
                    OnPropertyChanged();
                    
                    // Update diff results if selected snapshot is set
                    if (_selectedSnapshot != null && _comparisonSnapshot != null)
                    {
                        UpdateDiffResults();
                    }
                }
            }
        }

        /// <summary>
        /// Collection of diff results from snapshot comparison
        /// </summary>
        public ObservableCollection<RuleDiffViewModel> DiffResults { get; }

        /// <summary>
        /// Collection of added rules from comparison
        /// </summary>
        public ObservableCollection<StreamRule> AddedRules { get; }

        /// <summary>
        /// Collection of removed rules from comparison
        /// </summary>
        public ObservableCollection<StreamRule> RemovedRules { get; }

        /// <summary>
        /// Collection of modified rules from comparison
        /// </summary>
        public ObservableCollection<RuleChange> ModifiedRules { get; }

        /// <summary>
        /// Command to load history for a stream
        /// </summary>
        public ICommand LoadHistoryCommand { get; }

        /// <summary>
        /// Command to compare two snapshots
        /// </summary>
        public ICommand CompareSnapshotsCommand { get; }

        /// <summary>
        /// Command to restore a snapshot
        /// </summary>
        public ICommand RestoreSnapshotCommand { get; }

        public HistoryViewModel(SnapshotService snapshotService, SettingsService settingsService, string streamPath)
        {
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _streamPath = streamPath;

            Snapshots = new ObservableCollection<Snapshot>();
            DiffResults = new ObservableCollection<RuleDiffViewModel>();
            AddedRules = new ObservableCollection<StreamRule>();
            RemovedRules = new ObservableCollection<StreamRule>();
            ModifiedRules = new ObservableCollection<RuleChange>();

            LoadHistoryCommand = new RelayCommand(LoadHistory, CanLoadHistory);
            CompareSnapshotsCommand = new RelayCommand(CompareSnapshots, CanCompareSnapshots);
            RestoreSnapshotCommand = new RelayCommand(RestoreSnapshot, CanRestoreSnapshot);
        }

        /// <summary>
        /// Loads snapshot history for a stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void LoadHistory(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(_streamPath))
            {
                return;
            }

            RunWithProgressAsync(async () =>
            {
                // Clear existing snapshots (UI thread)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Snapshots.Clear();
                    DiffResults.Clear();
                    AddedRules.Clear();
                    RemovedRules.Clear();
                    ModifiedRules.Clear();
                    _selectedSnapshot = null;
                    _comparisonSnapshot = null;
                });

                // Load snapshots from service (background)
                var settings = await Task.Run(() => _settingsService.LoadSettings());
                var snapshots = await Task.Run(() => _snapshotService.LoadHistory(_streamPath, settings.HistoryStoragePath));

                // Add to observable collection (UI thread)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var snapshot in snapshots.OrderByDescending(s => s.Timestamp))
                    {
                        Snapshots.Add(snapshot);
                    }

                    // Select the first (newest) snapshot if available
                    if (Snapshots.Count > 0)
                    {
                        SelectedSnapshot = Snapshots[0];
                    }
                });
            }, "Loading history...");
        }

        /// <summary>
        /// Determines if history can be loaded
        /// </summary>
        private bool CanLoadHistory(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(_streamPath);
        }

        /// <summary>
        /// Compares two snapshots and displays the differences
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void CompareSnapshots(object? parameter)
        {
            try
            {
                if (_selectedSnapshot == null || _comparisonSnapshot == null)
                {
                    throw new InvalidOperationException("Both snapshots must be selected for comparison");
                }

                UpdateDiffResults();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error comparing snapshots: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determines if snapshots can be compared
        /// </summary>
        private bool CanCompareSnapshots(object? parameter)
        {
            return _selectedSnapshot != null && _comparisonSnapshot != null;
        }

        /// <summary>
        /// Updates the diff results based on selected and comparison snapshots
        /// </summary>
        private void UpdateDiffResults()
        {
            DiffResults.Clear();
            AddedRules.Clear();
            RemovedRules.Clear();
            ModifiedRules.Clear();

            if (_selectedSnapshot == null || _comparisonSnapshot == null)
            {
                return;
            }

            // Compare snapshots (older first, newer second)
            var older = _selectedSnapshot.Timestamp < _comparisonSnapshot.Timestamp 
                ? _selectedSnapshot 
                : _comparisonSnapshot;
            var newer = _selectedSnapshot.Timestamp < _comparisonSnapshot.Timestamp 
                ? _comparisonSnapshot 
                : _selectedSnapshot;

            var diff = _snapshotService.CompareSnapshots(older, newer);

            // Add added rules
            foreach (var rule in diff.AddedRules)
            {
                AddedRules.Add(rule);
                DiffResults.Add(new RuleDiffViewModel
                {
                    ChangeType = "Added",
                    RuleType = rule.Type,
                    Path = rule.Path,
                    RemapTarget = rule.RemapTarget,
                    SourceStream = rule.SourceStream
                });
            }

            // Add removed rules
            foreach (var rule in diff.RemovedRules)
            {
                RemovedRules.Add(rule);
                DiffResults.Add(new RuleDiffViewModel
                {
                    ChangeType = "Removed",
                    RuleType = rule.Type,
                    Path = rule.Path,
                    RemapTarget = rule.RemapTarget,
                    SourceStream = rule.SourceStream
                });
            }

            // Add modified rules
            foreach (var change in diff.ModifiedRules)
            {
                ModifiedRules.Add(change);
                DiffResults.Add(new RuleDiffViewModel
                {
                    ChangeType = "Modified",
                    RuleType = change.NewRule.Type,
                    Path = change.NewRule.Path,
                    RemapTarget = change.NewRule.RemapTarget,
                    SourceStream = change.NewRule.SourceStream,
                    OldRemapTarget = change.OldRule.RemapTarget,
                    OldSourceStream = change.OldRule.SourceStream
                });
            }
        }

        /// <summary>
        /// Restores the selected snapshot to the stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void RestoreSnapshot(object? parameter)
        {
            if (_selectedSnapshot == null)
            {
                System.Windows.MessageBox.Show("No snapshot selected for restore", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Show confirmation dialog
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to restore this snapshot?\n\nTimestamp: {_selectedSnapshot.Timestamp}\nThis will replace the current stream rules.", 
                "Confirm Restore", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                RunWithProgressAsync(async () =>
                {
                    // Restore snapshot (background)
                    await Task.Run(() => _snapshotService.RestoreSnapshot(_selectedSnapshot));

                    // Success message (UI thread)
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("Snapshot restored successfully", "Success", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    });
                }, "Restoring snapshot...");
            }
        }

        /// <summary>
        /// Determines if a snapshot can be restored
        /// </summary>
        private bool CanRestoreSnapshot(object? parameter)
        {
            return _selectedSnapshot != null;
        }

        /// <summary>
        /// Helper method to run an async action with a progress dialog
        /// </summary>
        private async void RunWithProgressAsync(Func<Task> action, string message)
        {
            var progressWindow = new Views.ProgressWindow(message);
            
            // Set owner if main window exists and is visible
            // Note: HistoryViewModel might be used in HistoryWindow, so owner could be HistoryWindow
            // But we don't have reference to the window here.
            // We'll rely on active window or main window.
            if (System.Windows.Application.Current?.MainWindow != null && 
                System.Windows.Application.Current.MainWindow.IsVisible)
            {
                progressWindow.Owner = System.Windows.Application.Current.MainWindow;
            }
            
            progressWindow.Show();

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                progressWindow.Close();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// View model for displaying rule differences
    /// </summary>
    public class RuleDiffViewModel
    {
        /// <summary>
        /// Type of change: Added, Removed, or Modified
        /// </summary>
        public string ChangeType { get; set; } = "";

        /// <summary>
        /// Type of rule: ignore or remap
        /// </summary>
        public string RuleType { get; set; } = "";

        /// <summary>
        /// Path of the rule
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Remap target (for remap rules)
        /// </summary>
        public string? RemapTarget { get; set; }

        /// <summary>
        /// Source stream that defined the rule
        /// </summary>
        public string? SourceStream { get; set; }

        /// <summary>
        /// Old remap target (for modified rules)
        /// </summary>
        public string? OldRemapTarget { get; set; }

        /// <summary>
        /// Old source stream (for modified rules)
        /// </summary>
        public string? OldSourceStream { get; set; }
    }
}