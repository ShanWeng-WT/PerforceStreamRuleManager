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
        private readonly ErrorMessageSanitizer _errorSanitizer;

        /// <summary>
        /// Gets the P4Service instance for external access (e.g., dialogs)
        /// </summary>
        public P4Service P4Service => _p4Service;

        /// <summary>
        /// Gets the SettingsService instance for external access (e.g., dialogs)
        /// </summary>
        public SettingsService SettingsService => _settingsService;

        private StreamNode? _selectedStream;
        private RuleViewModel? _selectedRule;
        private RuleViewMode _currentViewMode;
        private string _streamPathInput = "//depot/main";
        private string _connectionStatus = "Not Connected";
        private bool _hasUnsavedChanges;

        // Tracks original rules per stream path for change detection
        private readonly Dictionary<string, List<StreamRule>> _originalRulesPerStream = new();

        // Tracks original parents per stream path for change detection
        private readonly Dictionary<string, string?> _originalParentsPerStream = new();

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
                    _streamPathInput = value.TrimEnd('/');
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
                    OnPropertyChanged(nameof(SelectedStreamParentPath));
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
        /// Gets the parent path of the currently selected stream
        /// </summary>
        public string? SelectedStreamParentPath => SelectedStream?.ParentPath;

        /// <summary>
        /// Collection of available parent streams for selection
        /// </summary>
        public ObservableCollection<string> AvailableParentStreams { get; } = new();

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
        /// Command to open settings dialog
        /// </summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Command to open the application log file
        /// </summary>
        public ICommand OpenLogFileCommand { get; }

        /// <summary>
        /// Command to restore rules from history
        /// </summary>
        public ICommand RestoreCommand { get; }

        /// <summary>
        /// Command to change the parent stream
        /// </summary>
        public ICommand ChangeParentCommand { get; }

        public MainViewModel() : this(CreateDefaultServices())
        {
        }

        public MainViewModel(P4Service p4Service, SnapshotService snapshotService, SettingsService settingsService)
            : this((p4Service, snapshotService, settingsService))
        {
        }

        private MainViewModel((P4Service p4Service, SnapshotService snapshotService, SettingsService settingsService) services)
        {
            _p4Service = services.p4Service ?? throw new ArgumentNullException(nameof(services.p4Service));
            _snapshotService = services.snapshotService ?? throw new ArgumentNullException(nameof(services.snapshotService));
            _settingsService = services.settingsService ?? throw new ArgumentNullException(nameof(services.settingsService));
            _errorSanitizer = new ErrorMessageSanitizer(_p4Service.Logger);

            StreamHierarchy = new ObservableCollection<StreamNode>();
            DisplayedRemapRules = new ObservableCollection<RuleViewModel>();
            DisplayedIgnoreRules = new ObservableCollection<RuleViewModel>();
            _currentViewMode = RuleViewMode.All;

            LoadStreamCommand = new RelayCommand(LoadStreamHierarchy, CanLoadStream);
            AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
            EditRuleCommand = new RelayCommand(EditRule, CanEditRule);
            DeleteRuleCommand = new RelayCommand(DeleteRule, CanDeleteRule);
            SaveCommand = new RelayCommand(Save, CanSave);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenLogFileCommand = new RelayCommand(OpenLogFile);
            RestoreCommand = new RelayCommand(RestoreFromHistory, CanRestore);
            ChangeParentCommand = new RelayCommand(ChangeParent, CanChangeParent);

            InitializeConnection();
        }

        private static (P4Service, SnapshotService, SettingsService) CreateDefaultServices()
        {
            var loggingService = new LoggingService();
            var p4Service = new P4Service(loggingService);
            var settingsService = new SettingsService(loggingService);
            var snapshotService = new SnapshotService(p4Service, loggingService);
            return (p4Service, snapshotService, settingsService);
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
            finally
            {
                // Always force command manager to re-evaluate CanExecute after connection attempt
                // This ensures the Load button state updates correctly regardless of success/failure
                CommandManager.InvalidateRequerySuggested();
            }
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
                    // Clear existing hierarchy and original data tracking
                    StreamHierarchy.Clear();
                    _originalRulesPerStream.Clear();
                    _originalParentsPerStream.Clear();
                    HasUnsavedChanges = false;

                    // Add to observable collection and store original data
                    foreach (var node in hierarchy)
                    {
                        StreamHierarchy.Add(node);
                        StoreOriginalDataRecursive(node);
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
                string safeMessage = _errorSanitizer.SanitizeForUser(ex, "EditRule");
                System.Windows.MessageBox.Show(safeMessage, "Error",
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
                
                bool isRemap = string.Equals(ruleType, "remap", StringComparison.OrdinalIgnoreCase);

                // Remove the rule
                var removed = currentRules.RemoveAll(r => 
                    r.Type == ruleType && 
                    r.Path == rulePath && 
                    ((isRemap && r.RemapTarget == ruleRemapTarget) || !isRemap)) > 0;

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

            // Show save options dialog
            var saveOptionsDialog = new Views.SaveOptionsDialog();
            if (saveOptionsDialog.ShowDialog() != true)
            {
                return; // User cancelled
            }
            
            bool submitImmediately = saveOptionsDialog.SubmitImmediately;

            // Parameter can be a description string
            string description = $"[{Environment.UserName}] Update Stream Rule: {StreamPathInput}";

            if (parameter is string desc && !string.IsNullOrWhiteSpace(desc))
            {
                description = desc;
            }

            RunWithProgressAsync(async () =>
            {
                var streamNode = SelectedStream;
                var rules = streamNode.LocalRules;

                // Capture the root stream on the UI thread before entering background task
                var rootStream = StreamHierarchy.FirstOrDefault();
                if (rootStream == null)
                {
                    throw new InvalidOperationException("No stream hierarchy loaded");
                }

                // Capture pending parent changes on the UI thread
                var parentChanges = GetPendingParentChanges();

                await Task.Run(() =>
                {
                    // Update stream rules
                    _p4Service.UpdateStreamRules(streamNode.Path, rules);

                    // Update any changed parent streams
                    foreach (var parentChange in parentChanges)
                    {
                        _p4Service.UpdateStreamParent(parentChange.StreamPath, parentChange.NewParent);
                    }

                    // Create snapshot of the ENTIRE hierarchy (not just selected stream)
                    var snapshot = _snapshotService.CreateHierarchySnapshot(rootStream);

                    // Get history storage path from settings
                    var settings = _settingsService.LoadSettings();
                    string storagePath = settings.HistoryStoragePath;

                    // If storage path is relative, append it to StreamPathInput
                    if (!string.IsNullOrWhiteSpace(storagePath) && !storagePath.StartsWith("//") && !string.IsNullOrWhiteSpace(StreamPathInput))
                    {
                        string root = StreamPathInput.TrimEnd('/');
                        storagePath = $"{root}/{storagePath.TrimStart('/')}";
                    }

                    // Try to auto-detect workspace for this stream if one isn't explicitly set
                    _p4Service.AutoDetectAndSwitchWorkspace(StreamPathInput);

                    // Save snapshot - P4 versioning will track history
                    _snapshotService.SaveSnapshot(snapshot, StreamPathInput, storagePath, description, submitImmediately);
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update the original parents tracking after successful save
                    foreach (var parentChange in parentChanges)
                    {
                        _originalParentsPerStream[parentChange.StreamPath] = parentChange.NewParent;
                    }

                    HasUnsavedChanges = false;
                    string message = submitImmediately
                        ? $"Stream {streamNode.Path} saved and submitted successfully."
                        : $"Stream {streamNode.Path} saved. Snapshot file left in pending changelist.";
                    System.Windows.MessageBox.Show(message, "Success",
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
        /// Opens the settings dialog
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void OpenSettings(object? parameter)
        {
            try
            {
                var settingsDialog = new Views.SettingsDialog(_settingsService, _p4Service);
                if (settingsDialog.ShowDialog() == true)
                {
                    InitializeConnection();
                }
            }
            catch (Exception ex)
            {
                string safeMessage = _errorSanitizer.SanitizeForUser(ex, "OpenSettings");
                System.Windows.MessageBox.Show(safeMessage, "Error",
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
                string safeMessage = _errorSanitizer.SanitizeForUser(ex, "OpenLogFile");
                System.Windows.MessageBox.Show(safeMessage, "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Changes the parent stream of the selected stream
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void ChangeParent(object? parameter)
        {
            if (SelectedStream == null)
            {
                System.Windows.MessageBox.Show("No stream selected", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new Views.ParentStreamDialog(
                    _p4Service,
                    SelectedStream.Path,
                    SelectedStream.ParentPath);

                if (dialog.ShowDialog() == true)
                {
                    string? newParent = dialog.SelectedParentPath;

                    // Update the stream node's parent path
                    SelectedStream.ParentPath = newParent ?? string.Empty;

                    // Mark as unsaved
                    HasUnsavedChanges = true;

                    // Notify UI of parent path change
                    OnPropertyChanged(nameof(SelectedStreamParentPath));
                }
            }
            catch (Exception ex)
            {
                string safeMessage = _errorSanitizer.SanitizeForUser(ex, "ChangeParent");
                System.Windows.MessageBox.Show(safeMessage, "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determines if the parent can be changed
        /// </summary>
        private bool CanChangeParent(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Gets all pending parent changes across all streams in the hierarchy
        /// </summary>
        /// <returns>List of parent changes</returns>
        public List<ParentChangeInfo> GetPendingParentChanges()
        {
            var changes = new List<ParentChangeInfo>();

            foreach (var node in StreamHierarchy)
            {
                CollectParentChangesRecursive(node, changes);
            }

            return changes;
        }

        /// <summary>
        /// Recursively collects parent changes for a stream node and all its children
        /// </summary>
        private void CollectParentChangesRecursive(StreamNode node, List<ParentChangeInfo> changes)
        {
            if (node == null) return;

            // Get original parent for this stream
            _originalParentsPerStream.TryGetValue(node.Path, out var originalParent);

            // Normalize empty strings to null for comparison
            string? currentParent = string.IsNullOrEmpty(node.ParentPath) ? null : node.ParentPath;
            originalParent = string.IsNullOrEmpty(originalParent) ? null : originalParent;

            // Check if parent has changed
            if (!string.Equals(originalParent, currentParent, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new ParentChangeInfo
                {
                    StreamPath = node.Path,
                    OriginalParent = originalParent,
                    NewParent = currentParent
                });
            }

            // Recurse into children
            foreach (var child in node.Children)
            {
                CollectParentChangesRecursive(child, changes);
            }
        }

        /// <summary>
        /// Restores rules from a previous snapshot version
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        private void RestoreFromHistory(object? parameter)
        {
            if (SelectedStream == null)
            {
                System.Windows.MessageBox.Show("Please select a stream first.", "No Stream Selected", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            RunWithProgressAsync(async () =>
            {
                var streamNode = SelectedStream;
                var settings = _settingsService.LoadSettings();
                string storagePath = settings.HistoryStoragePath;

                // Resolve relative storage path
                if (!string.IsNullOrWhiteSpace(storagePath) && !storagePath.StartsWith("//") && !string.IsNullOrWhiteSpace(StreamPathInput))
                {
                    string root = StreamPathInput.TrimEnd('/');
                    storagePath = $"{root}/{storagePath.TrimStart('/')}";
                }

                // Get snapshot file path
                string snapshotFilePath = _snapshotService.GetSnapshotFilePath(StreamPathInput, storagePath);

                // Get file history
                var revisions = await Task.Run(() => _p4Service.GetFileHistory(snapshotFilePath));

                if (revisions.Count == 0)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            $"No history found for this stream.\n\nSnapshot file: {snapshotFilePath}\n\nYou need to save at least once before you can restore from history.", 
                            "No History", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    });
                    return;
                }

                // Show restore dialog on UI thread
                Models.FileRevisionInfo? selectedRevision = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.RestoreDialog(streamNode.Path, revisions);
                    if (System.Windows.Application.Current?.MainWindow != null)
                    {
                        dialog.Owner = System.Windows.Application.Current.MainWindow;
                    }
                    
                    if (dialog.ShowDialog() == true)
                    {
                        selectedRevision = dialog.SelectedRevision;
                    }
                });

                if (selectedRevision == null)
                {
                    return; // User cancelled
                }

                // Load snapshot at selected revision
                var revision = selectedRevision;
                var jsonContent = await Task.Run(() => _p4Service.ReadDepotFileAtRevision(snapshotFilePath, revision.Revision));
                var snapshot = _snapshotService.LoadSnapshot(jsonContent);

                // Get the root stream to restore all streams in the hierarchy
                var rootStream = StreamHierarchy.FirstOrDefault();
                if (rootStream == null)
                {
                    throw new InvalidOperationException("No stream hierarchy loaded");
                }

                // Restore rules and parent info for all streams in the hierarchy
                int totalStreamsRestored = 0;
                int totalRulesRestored = 0;
                int totalParentsRestored = 0;
                RestoreHierarchyData(rootStream, snapshot, ref totalStreamsRestored, ref totalRulesRestored, ref totalParentsRestored);

                // Update UI on dispatcher thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Mark as unsaved
                    HasUnsavedChanges = true;

                    // Refresh display
                    RefreshRuleDisplay();
                    OnPropertyChanged(nameof(SelectedStreamParentPath));

                    string parentInfo = totalParentsRestored > 0
                        ? $" and {totalParentsRestored} parent assignment(s)"
                        : "";
                    System.Windows.MessageBox.Show(
                        $"Restored {totalRulesRestored} rule(s){parentInfo} across {totalStreamsRestored} stream(s) from revision #{revision.Revision}.\n\nClick Save to apply these changes to Perforce.",
                        "Restore Complete",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                });
            }, "Loading history...");
        }

        /// <summary>
        /// Determines if restore can be executed
        /// </summary>
        private bool CanRestore(object? parameter)
        {
            return _p4Service.IsConnected && SelectedStream != null;
        }

        /// <summary>
        /// Recursively restores rules and parent info for a stream node and all its children from a snapshot
        /// </summary>
        private void RestoreHierarchyData(StreamNode node, Models.Snapshot snapshot, ref int totalStreamsRestored, ref int totalRulesRestored, ref int totalParentsRestored)
        {
            // Get rules for this stream from snapshot
            var rules = snapshot.GetRulesForStream(node.Path);

            // Only update rules if there are rules in the snapshot for this stream
            // (empty list means the stream was captured but had no rules)
            if (snapshot.StreamRules?.ContainsKey(node.Path) == true ||
                snapshot.StreamRules?.Keys.Any(k => string.Equals(k, node.Path, StringComparison.OrdinalIgnoreCase)) == true ||
                rules.Count > 0)
            {
                node.LocalRules.Clear();
                foreach (var rule in rules)
                {
                    node.LocalRules.Add(rule);
                }
                totalStreamsRestored++;
                totalRulesRestored += rules.Count;
            }

            // Restore parent info if snapshot has it
            if (snapshot.HasParentInfo)
            {
                var parent = snapshot.GetParentForStream(node.Path);
                // Only update if the snapshot explicitly has info for this stream
                if (snapshot.StreamParents?.ContainsKey(node.Path) == true ||
                    snapshot.StreamParents?.Keys.Any(k => string.Equals(k, node.Path, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    node.ParentPath = parent ?? string.Empty;
                    totalParentsRestored++;
                }
            }

            // Recurse into children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    RestoreHierarchyData(child, snapshot, ref totalStreamsRestored, ref totalRulesRestored, ref totalParentsRestored);
                }
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
                string safeMessage = _errorSanitizer.SanitizeForUser(ex, "RunWithProgressAsync");
                System.Windows.MessageBox.Show(safeMessage, "Error",
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
        /// Recursively stores original rules and parent info for a stream node and all its children
        /// </summary>
        private void StoreOriginalDataRecursive(StreamNode node)
        {
            if (node == null) return;

            // Store a deep copy of local rules for this stream
            _originalRulesPerStream[node.Path] = node.LocalRules
                .Select(r => new StreamRule(r.Type, r.Path, r.RemapTarget, r.SourceStream))
                .ToList();

            // Store the original parent path for this stream
            _originalParentsPerStream[node.Path] = string.IsNullOrEmpty(node.ParentPath) ? null : node.ParentPath;

            // Recurse into children
            foreach (var child in node.Children)
            {
                StoreOriginalDataRecursive(child);
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
