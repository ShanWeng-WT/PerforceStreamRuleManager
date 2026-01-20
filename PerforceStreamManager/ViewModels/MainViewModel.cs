using System;
using System.Collections.Generic;
using System.IO;
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
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly P4Service _p4Service;
        private readonly SnapshotService _snapshotService;
        private readonly SettingsService _settingsService;
        
        private StreamNode? _selectedStream;
        private RuleViewModel? _selectedRule;
        private RuleViewMode _currentViewMode;
        private string _streamPathInput = "//depot/main";
        private string _connectionStatus = "Not Connected";
        private bool _hasUnsavedChanges;

        // Tracks original rules per stream path for change detection
        private readonly Dictionary<string, List<StreamRule>> _originalRulesPerStream = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Indicates if there are unsaved rule changes
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                if (_hasUnsavedChanges != value)
                {
                    _hasUnsavedChanges = value;
                    OnPropertyChanged();
                    // Force re-evaluation of CanSave
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Input for the stream path to load
        /// </summary>
        public string StreamPathInput
        {
            get => _streamPathInput;
            set
            {
                if (_streamPathInput != value)
                {
                    _streamPathInput = value;
                    OnPropertyChanged();
                    // Force re-evaluation of CanLoadStream
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Status message for Perforce connection
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Collection of root stream nodes in the hierarchy
        /// </summary>
        public ObservableCollection<StreamNode> StreamHierarchy { get; }

        /// <summary>
        /// Currently selected stream node
        /// </summary>
        public StreamNode? SelectedStream
        {
            get => _selectedStream;
            set
            {
                if (_selectedStream != value)
                {
                    _selectedStream = value;
                    OnPropertyChanged();
                    RefreshRuleDisplay();
                }
            }
        }

        /// <summary>
        /// Currently selected rule
        /// </summary>
        public RuleViewModel? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (_selectedRule != value)
                {
                    _selectedRule = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Collection of remap rules to display based on current view mode
        /// </summary>
        public ObservableCollection<RuleViewModel> DisplayedRemapRules { get; }

        /// <summary>
        /// Collection of ignore rules to display based on current view mode
        /// </summary>
        public ObservableCollection<RuleViewModel> DisplayedIgnoreRules { get; }

        /// <summary>
        /// Current rule view mode (Local, Inherited, All)
        /// </summary>
        public RuleViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (_currentViewMode != value)
                {
                    _currentViewMode = value;
                    OnPropertyChanged();
                    RefreshRuleDisplay();
                }
            }
        }

        // Properties for radio button binding
        public bool IsLocalViewMode
        {
            get => CurrentViewMode == RuleViewMode.Local;
            set { if (value) CurrentViewMode = RuleViewMode.Local; }
        }

        public bool IsInheritedViewMode
        {
            get => CurrentViewMode == RuleViewMode.Inherited;
            set { if (value) CurrentViewMode = RuleViewMode.Inherited; }
        }

        public bool IsAllViewMode
        {
            get => CurrentViewMode == RuleViewMode.All;
            set { if (value) CurrentViewMode = RuleViewMode.All; }
        }

        /// <summary>
        /// Command to load stream hierarchy
        /// </summary>
        public ICommand LoadStreamCommand { get; }

        /// <summary>
        /// Command to add a new rule
        /// </summary>
        public ICommand AddRuleCommand { get; }

        /// <summary>
        /// Command to edit an existing rule
        /// </summary>
        public ICommand EditRuleCommand { get; }

        /// <summary>
        /// Command to delete a rule
        /// </summary>
        public ICommand DeleteRuleCommand { get; }

        /// <summary>
        /// Command to save changes and create a snapshot
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// Command to open history viewer
        /// </summary>
        public ICommand OpenHistoryCommand { get; }

        /// <summary>
        /// Command to open settings dialog
        /// </summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Command to open the application log file
        /// </summary>
        public ICommand OpenLogFileCommand { get; }

        public MainViewModel(P4Service p4Service, SnapshotService snapshotService, SettingsService settingsService)
        {
            _p4Service = p4Service ?? throw new ArgumentNullException(nameof(p4Service));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            StreamHierarchy = new ObservableCollection<StreamNode>();
            DisplayedRemapRules = new ObservableCollection<RuleViewModel>();
            DisplayedIgnoreRules = new ObservableCollection<RuleViewModel>();
            _currentViewMode = RuleViewMode.All;

            LoadStreamCommand = new RelayCommand(LoadStreamHierarchy, CanLoadStream);
            AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
            EditRuleCommand = new RelayCommand(EditRule, CanEditRule);
            DeleteRuleCommand = new RelayCommand(DeleteRule, CanDeleteRule);
            SaveCommand = new RelayCommand(Save, CanSave);
            OpenHistoryCommand = new RelayCommand(OpenHistory, CanOpenHistory);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenLogFileCommand = new RelayCommand(OpenLogFile);

            InitializeConnection();
        }

        public MainViewModel()
        {
            var loggingService = new LoggingService();
            _p4Service = new P4Service(loggingService);
            _settingsService = new SettingsService(loggingService);
            _snapshotService = new SnapshotService(_p4Service, loggingService);
            
            StreamHierarchy = new ObservableCollection<StreamNode>();
            DisplayedRemapRules = new ObservableCollection<RuleViewModel>();
            DisplayedIgnoreRules = new ObservableCollection<RuleViewModel>();
            _currentViewMode = RuleViewMode.All;

            LoadStreamCommand = new RelayCommand(LoadStreamHierarchy, CanLoadStream);
            AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
            EditRuleCommand = new RelayCommand(EditRule, CanEditRule);
            DeleteRuleCommand = new RelayCommand(DeleteRule, CanDeleteRule);
            SaveCommand = new RelayCommand(Save, CanSave);
            OpenHistoryCommand = new RelayCommand(OpenHistory, CanOpenHistory);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenLogFileCommand = new RelayCommand(OpenLogFile);

            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                var settings = _settingsService.LoadSettings();

                // Set the last used stream if available
                if (!string.IsNullOrWhiteSpace(settings.LastUsedStream))
                {
                    StreamPathInput = settings.LastUsedStream;
                }

                // Basic validation before attempting connection
                if (settings?.Connection != null && 
                    !string.IsNullOrWhiteSpace(settings.Connection.Server) &&
                    !string.IsNullOrWhiteSpace(settings.Connection.User))
                {
                    ConnectionStatus = $"Connecting to {settings.Connection.Server}...";
                    _p4Service.Connect(settings.Connection);
                    ConnectionStatus = $"Connected to {settings.Connection.Server} ({settings.Connection.User})";
                }
                else
                {
                    ConnectionStatus = "Not Configured (Go to File > Settings)";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Connection Failed";
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
            
            // Force command manager to re-evaluate CanExecute
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Loads the stream hierarchy from Perforce
        /// </summary>
        /// <param name="parameter">Optional parameter (unused)</param>
        private void LoadStreamHierarchy(object? parameter)
        {
            string streamPath = StreamPathInput;
            
            if (string.IsNullOrWhiteSpace(streamPath))
            {
                return;
            }

            RunWithProgressAsync(async () =>
            {
                // Load hierarchy from P4Service (background)
                var hierarchy = await Task.Run(() => _p4Service.GetStreamHierarchy(streamPath));

                // Save last used stream if load was successful
                try
                {
                    var settings = _settingsService.LoadSettings();
                    if (settings.LastUsedStream != streamPath)
                    {
                        settings.LastUsedStream = streamPath;
                        _settingsService.SaveSettings(settings);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save last used stream: {ex.Message}");
                }

                // Update UI
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Clear existing hierarchy and original rules tracking
                    StreamHierarchy.Clear();
                    _originalRulesPerStream.Clear();
                    HasUnsavedChanges = false;

                    // Add to observable collection and store original rules
                    foreach (var node in hierarchy)
                    {
                        StreamHierarchy.Add(node);
                        StoreOriginalRulesRecursive(node);
                    }

                    // Select the first stream if available
                    if (StreamHierarchy.Count > 0)
                    {
                        SelectedStream = StreamHierarchy[0];
                    }
                });
            }, $"Loading stream hierarchy for {streamPath}...");
        }

        /// <summary>
        /// Determines if the LoadStream command can execute
        /// </summary>
        private bool CanLoadStream(object? parameter)
        {
            return _p4Service.IsConnected && !string.IsNullOrWhiteSpace(StreamPathInput);
        }

        /// <summary>
        /// Refreshes the displayed rules based on current view mode and selected stream
        /// </summary>
        private void RefreshRuleDisplay()
        {
            DisplayedRemapRules.Clear();
            DisplayedIgnoreRules.Clear();

            if (SelectedStream == null)
            {
                return;
            }

            // Get rules based on view mode
            var rules = CurrentViewMode switch
            {
                RuleViewMode.Local => SelectedStream.GetLocalRules(),
                RuleViewMode.Inherited => SelectedStream.GetInheritedRules(),
                RuleViewMode.All => SelectedStream.GetAllRules(),
                _ => SelectedStream.GetAllRules()
            };

            // Convert to RuleViewModel and partition by type
            foreach (var rule in rules)
            {
                var ruleViewModel = new RuleViewModel
                {
                    RuleType = rule.Type,
                    Path = rule.Path,
                    RemapTarget = rule.RemapTarget ?? "",
                    SourceStream = rule.SourceStream ?? "",
                    IsInherited = rule.SourceStream != SelectedStream.Path,
                    IsLocal = rule.SourceStream == SelectedStream.Path
                };

                // Partition into appropriate collection based on type (case-insensitive)
                if (string.Equals(rule.Type, "remap", StringComparison.OrdinalIgnoreCase))
                {
                    DisplayedRemapRules.Add(ruleViewModel);
                }
                else if (string.Equals(rule.Type, "ignore", StringComparison.OrdinalIgnoreCase))
                {
                    DisplayedIgnoreRules.Add(ruleViewModel);
                }
            }
        }

        /// <summary>
        /// Adds a new rule to the selected stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void AddRule(object? parameter)
        {
            if (SelectedStream == null)
            {
                System.Windows.MessageBox.Show("No stream selected", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var dialog = new Views.RuleDialog(_p4Service, SelectedStream.Path, StreamPathInput);
            if (dialog.ShowDialog() == true)
            {
                var newRule = new StreamRule
                {
                    Type = dialog.RuleType,
                    Path = NormalizeRulePath(dialog.Path),
                    RemapTarget = SanitizePath(dialog.RemapTarget),
                    SourceStream = SelectedStream.Path
                };

                // Add the new rule to local collection
                SelectedStream.LocalRules.Add(newRule);

                // Mark as unsaved
                HasUnsavedChanges = true;

                // Refresh the display
                RefreshRuleDisplay();
                
                // Optional: Provide feedback (can be removed if too intrusive)
                // System.Windows.MessageBox.Show("Rule added. Click 'Save' to apply changes.", "Rule Added", 
                //     System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Determines if a rule can be added
        /// </summary>
        private bool CanAddRule(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Edits an existing rule in the selected stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void EditRule(object? parameter)
        {
            try
            {
                if (SelectedStream == null)
                {
                    System.Windows.MessageBox.Show("No stream selected", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (SelectedRule == null)
                {
                    System.Windows.MessageBox.Show("No rule selected", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Can only edit local rules
                if (!SelectedRule.IsLocal)
                {
                    System.Windows.MessageBox.Show("Cannot edit inherited rules. Please edit in the source stream.", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Create existing rule object
                var existingRule = new StreamRule
                {
                    Type = SelectedRule.RuleType,
                    Path = SelectedRule.Path,
                    RemapTarget = SelectedRule.RemapTarget,
                    SourceStream = SelectedRule.SourceStream
                };

                var dialog = new Views.RuleDialog(_p4Service, SelectedStream.Path, StreamPathInput, existingRule: existingRule);
                if (dialog.ShowDialog() == true)
                {
                    var newRule = new StreamRule
                    {
                        Type = dialog.RuleType,
                        Path = NormalizeRulePath(dialog.Path),
                        RemapTarget = SanitizePath(dialog.RemapTarget),
                        SourceStream = SelectedStream.Path
                    };

                    // Get current local rules
                    var currentRules = SelectedStream.LocalRules;

                    // Find and replace the old rule
                    var index = currentRules.FindIndex(r => 
                        string.Equals(r.Type, existingRule.Type, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(r.Path, existingRule.Path, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(r.RemapTarget ?? "", existingRule.RemapTarget ?? "", StringComparison.OrdinalIgnoreCase));

                    if (index >= 0)
                    {
                        currentRules[index] = newRule;
                        HasUnsavedChanges = true;
                        RefreshRuleDisplay();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Rule not found in stream (it may have been modified externally).", "Error", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"An error occurred while editing the rule: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determines if a rule can be edited
        /// </summary>
        private bool CanEditRule(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Deletes a rule from the selected stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void DeleteRule(object? parameter)
        {
            if (SelectedStream == null)
            {
                System.Windows.MessageBox.Show("No stream selected", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (SelectedRule == null)
            {
                System.Windows.MessageBox.Show("No rule selected", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Can only delete local rules
            if (!SelectedRule.IsLocal)
            {
                System.Windows.MessageBox.Show("Cannot delete inherited rules. Please delete in the source stream.", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Confirm deletion
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete this rule?\n\nType: {SelectedRule.RuleType}\nPath: {SelectedRule.Path}", 
                "Confirm Delete", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // Capture rule details before async operation
                var ruleType = SelectedRule.RuleType;
                var rulePath = SelectedRule.Path;
                var ruleRemapTarget = SelectedRule.RemapTarget;

                // Get current local rules
                var currentRules = SelectedStream.LocalRules;

                // Remove the rule
                var removed = currentRules.RemoveAll(r => 
                    r.Type == ruleType && 
                    r.Path == rulePath && 
                    r.RemapTarget == ruleRemapTarget) > 0;

                if (removed)
                {
                    HasUnsavedChanges = true;
                    RefreshRuleDisplay();
                }
                else
                {
                    System.Windows.MessageBox.Show("Rule not found in stream", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Determines if a rule can be deleted
        /// </summary>
        private bool CanDeleteRule(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Saves changes to the stream and creates a snapshot
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void Save(object? parameter)
        {
            if (SelectedStream == null)
            {
                return;
            }

            // Parameter should be a tuple of (createdBy, description)
            string createdBy = Environment.UserName;
            string? description = null;

            if (parameter is (string user, string desc))
            {
                createdBy = user;
                description = desc;
            }
            else if (parameter is string userOnly)
            {
                createdBy = userOnly;
            }

            // Capture StreamPathInput on UI thread
            string streamPathInput = StreamPathInput;

            RunWithProgressAsync(async () =>
            {
                var streamNode = SelectedStream;
                var rules = streamNode.LocalRules;

                await Task.Run(() =>
                {
                    // Update stream rules
                    _p4Service.UpdateStreamRules(streamNode.Path, rules);

                    // Create snapshot
                    var snapshot = _snapshotService.CreateSnapshot(streamNode, createdBy, description);

                    // Force StreamPath to match StreamPathInput per requirement
                    if (!string.IsNullOrWhiteSpace(streamPathInput))
                    {
                        snapshot.StreamPath = streamPathInput;
                    }

                    // Get history storage path from settings
                    var settings = _settingsService.LoadSettings();
                    string storagePath = settings.HistoryStoragePath;

                    // If storage path is relative, append it to StreamPathInput
                    if (!string.IsNullOrWhiteSpace(storagePath) && !storagePath.StartsWith("//") && !string.IsNullOrWhiteSpace(streamPathInput))
                    {
                        string root = streamPathInput.TrimEnd('/');
                        storagePath = $"{root}/{storagePath.TrimStart('/')}";
                    }

                    // Try to auto-detect workspace for this stream if one isn't explicitly set
                    // This ensures we can resolve local paths for 'p4 where'
                    _p4Service.AutoDetectAndSwitchWorkspace(streamPathInput);

                    // Save snapshot
                    _snapshotService.SaveSnapshot(snapshot, storagePath);
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    HasUnsavedChanges = false;
                    System.Windows.MessageBox.Show($"Stream {streamNode.Path} saved successfully.", "Success",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                });
            }, "Saving stream...");
        }

        /// <summary>
        /// Determines if changes can be saved
        /// </summary>
        private bool CanSave(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Opens the history viewer for the selected stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void OpenHistory(object? parameter)
        {
            try
            {
                if (SelectedStream == null)
                {
                    System.Windows.MessageBox.Show("No stream selected", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var historyViewModel = new HistoryViewModel(_snapshotService, _settingsService, SelectedStream.Path);
                var historyWindow = new Views.HistoryWindow(historyViewModel);
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening history: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determines if history can be opened
        /// </summary>
        private bool CanOpenHistory(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Opens the settings dialog
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void OpenSettings(object? parameter)
        {
            try
            {
                var settingsDialog = new Views.SettingsDialog(_settingsService);
                if (settingsDialog.ShowDialog() == true)
                {
                    InitializeConnection();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening settings: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Opens the application log file
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void OpenLogFile(object? parameter)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PerforceStreamManager", "application.log");
                
                if (File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show($"Log file not found at: {logPath}", "Information", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening log file: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Helper method to run an async action with a progress dialog
        /// </summary>
        private async void RunWithProgressAsync(Func<Task> action, string message)
        {
            var progressWindow = new Views.ProgressWindow(message);
            
            // Set owner if main window exists and is visible
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
                System.Windows.MessageBox.Show($"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                progressWindow.Close();
            }
        }

        /// <summary>
        /// Normalizes a rule path by removing the stream root and sanitizing slashes.
        /// Use this for the left-side 'Path' of a rule which must be relative.
        /// </summary>
        private string NormalizeRulePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            // Remove stream root if present (case-insensitive)
            if (SelectedStream != null && !string.IsNullOrWhiteSpace(SelectedStream.Path))
            {
                string streamRoot = SelectedStream.Path;
                int idx = path.IndexOf(streamRoot, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    path = path.Remove(idx, streamRoot.Length);
                }
            }

            // Standardize slashes
            path = path.Replace("\\", "/");
            while (path.Contains("//"))
            {
                path = path.Replace("//", "/");
            }

            // Relative paths should not start with slash
            return path.TrimStart('/');
        }

        /// <summary>
        /// Helper to sanitize Perforce paths (fix double slashes, backslashes).
        /// Use this for absolute paths like RemapTarget.
        /// </summary>
        private string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            // Normalize backslashes
            path = path.Replace("\\", "/");

            // Check if it starts with // (depot path)
            bool isDepotPath = path.StartsWith("//");

            // Collapse multiple slashes
            while (path.Contains("//"))
            {
                path = path.Replace("//", "/");
            }

            // Restore starting // for depot paths
            if (isDepotPath)
            {
                if (path.StartsWith("/"))
                    path = "/" + path;
                else
                    path = "//" + path;
            }

            return path;
        }

        /// <summary>
        /// Recursively stores original rules for a stream node and all its children
        /// </summary>
        private void StoreOriginalRulesRecursive(StreamNode node)
        {
            if (node == null) return;

            // Store a deep copy of local rules for this stream
            _originalRulesPerStream[node.Path] = node.LocalRules
                .Select(r => new StreamRule(r.Type, r.Path, r.RemapTarget, r.SourceStream))
                .ToList();

            // Recurse into children
            foreach (var child in node.Children)
            {
                StoreOriginalRulesRecursive(child);
            }
        }

        /// <summary>
        /// Gets all pending rule changes across all streams in the hierarchy
        /// </summary>
        /// <returns>List of rule changes (added, modified, deleted)</returns>
        public List<RuleChangeInfo> GetPendingChanges()
        {
            var changes = new List<RuleChangeInfo>();

            foreach (var node in StreamHierarchy)
            {
                CollectChangesRecursive(node, changes);
            }

            return changes;
        }

        /// <summary>
        /// Recursively collects changes for a stream node and all its children
        /// </summary>
        private void CollectChangesRecursive(StreamNode node, List<RuleChangeInfo> changes)
        {
            if (node == null) return;

            // Get original rules for this stream
            if (!_originalRulesPerStream.TryGetValue(node.Path, out var originalRules))
            {
                originalRules = new List<StreamRule>();
            }

            var currentRules = node.LocalRules;

            // Find added rules (in current but not in original)
            foreach (var current in currentRules)
            {
                bool existsInOriginal = originalRules.Any(o =>
                    string.Equals(o.Type, current.Type, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(o.Path, current.Path, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(o.RemapTarget ?? "", current.RemapTarget ?? "", StringComparison.OrdinalIgnoreCase));

                if (!existsInOriginal)
                {
                    changes.Add(new RuleChangeInfo
                    {
                        ChangeType = RuleChangeType.Added,
                        StreamPath = node.Path,
                        Rule = current
                    });
                }
            }

            // Find deleted rules (in original but not in current)
            foreach (var original in originalRules)
            {
                bool existsInCurrent = currentRules.Any(c =>
                    string.Equals(c.Type, original.Type, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Path, original.Path, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.RemapTarget ?? "", original.RemapTarget ?? "", StringComparison.OrdinalIgnoreCase));

                if (!existsInCurrent)
                {
                    changes.Add(new RuleChangeInfo
                    {
                        ChangeType = RuleChangeType.Deleted,
                        StreamPath = node.Path,
                        Rule = original
                    });
                }
            }

            // Recurse into children
            foreach (var child in node.Children)
            {
                CollectChangesRecursive(child, changes);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Enum for rule view modes
    /// </summary>
    public enum RuleViewMode
    {
        Local,
        Inherited,
        All
    }
}
