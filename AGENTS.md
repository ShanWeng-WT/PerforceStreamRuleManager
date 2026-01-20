# Perforce Stream Rule Manager - Agent Development Guide

## Overview
WPF desktop application for managing Perforce stream hierarchies using P4API.NET. Built with .NET 8.0, MVVM pattern, and NUnit for testing.

## Documentation
- **Requirements**: `.opencode/specs/perforce-stream-manager/requirements.md`
- **Design Specs**: `.opencode/specs/perforce-stream-manager/design.md`
- **Subagents**: `.opencode/subagents/` - Specialized domain experts

## Subagent Auto-Orchestration

**IMPORTANT**: When tasks are requested, automatically delegate to specialized subagents by reading the appropriate file and following its instructions. This ensures consistent, expert-level implementation across all domains.

### Auto-Delegation Rules

Apply these rules automatically based on the task type:

**XAML/UI Tasks** → Read `.opencode/subagents/wpf-ui-designer.md`
- Creating/editing `.xaml` files (MainWindow, dialogs, user controls)
- Implementing data binding (TwoWay, OneWay, UpdateSourceTrigger)
- Designing TreeView/DataGrid layouts with HierarchicalDataTemplate
- Creating WPF dialogs and windows
- Implementing INotifyPropertyChanged in Views

**Perforce Integration Tasks** → Read `.opencode/subagents/p4-integration-specialist.md`
- Implementing `P4Service` methods
- Stream operations (GetStream, GetStreamHierarchy, GetStreamRules)
- Depot file operations (ReadDepotFile, WriteDepotFile)
- P4API.NET integration and connection management
- Stream hierarchy building algorithms
- Handling P4Exceptions

**ViewModel/MVVM Tasks** → Read `.opencode/subagents/mvvm-architect.md`
- Creating ViewModels (MainViewModel, RuleViewModel, HistoryViewModel)
- Implementing INotifyPropertyChanged and PropertyChanged events
- Creating RelayCommand and ICommand implementations
- ObservableCollection setup and management
- Command CanExecute logic
- Async operations in ViewModels

**Testing Tasks** → Read `.opencode/subagents/test-generator.md`
- Writing NUnit unit tests for services/ViewModels
- Creating property-based tests with FsCheck (all 19 properties)
- Test fixture setup and teardown
- Creating mock implementations (MockP4Service)
- Test coverage analysis
- Integration tests

**JSON/Data Persistence Tasks** → Read `.opencode/subagents/json-data-manager.md`
- Implementing `SnapshotService` (CreateSnapshot, SaveSnapshot, LoadHistory)
- Implementing `SettingsService` (LoadSettings, SaveSettings)
- JSON serialization with System.Text.Json
- Round-trip validation
- Retention policy implementation
- DateTime handling

**Error Handling/Logging Tasks** → Read `.opencode/subagents/error-handler.md`
- Implementing `LoggingService`
- Creating custom exception types (ConnectionException, AuthenticationException)
- Error message design (user-friendly messages)
- Audit trail implementation (LogAudit)
- Global exception handlers (App.xaml.cs)
- P4Exception categorization and handling

**Debugging/Diagnostics Tasks** → Read `.opencode/subagents/debugger.md`
- Diagnosing binding failures ("TreeView not displaying", "UI not updating")
- Investigating P4Exception errors (connection timeout, authentication failures)
- Fixing UI freezes and performance bottlenecks
- Debugging PropertyChanged and command CanExecute issues
- Detecting memory leaks and disposal problems
- Troubleshooting async/threading issues (cross-thread violations)
- Adding diagnostic instrumentation and logging
- Performance profiling and optimization

### Multi-Domain Tasks

For complex features spanning multiple domains, coordinate subagents in sequence:

**Example: "Implement Load Stream Hierarchy Feature"**
1. **p4-integration-specialist**: Implement `P4Service.GetStreamHierarchy()`
2. **error-handler**: Add error handling and logging to the method
3. **mvvm-architect**: Create `LoadStreamCommand` in `MainViewModel`
4. **wpf-ui-designer**: Add TreeView to MainWindow.xaml with proper binding
5. **test-generator**: Create unit tests and property-based tests

**Example: "Debug TreeView Not Displaying Data"**
1. **debugger**: Diagnose the issue (binding, DataContext, PropertyChanged)
2. **wpf-ui-designer**: Fix XAML binding if needed
3. **mvvm-architect**: Fix ViewModel PropertyChanged if needed
4. **test-generator**: Add test to prevent regression

### Orchestration Priority

1. **Read the subagent file** for the primary domain
2. **Follow all patterns and examples** in the subagent specification
3. **Apply error handling** using error-handler patterns
4. **Coordinate with other subagents** as needed for dependencies
5. **Reference requirements/design docs** for context

## Build & Test Commands

### Build
```bash
# Build entire solution
dotnet build PerforceStreamManager.sln

# Build in Release mode
dotnet build PerforceStreamManager.sln -c Release

# Build specific project
dotnet build PerforceStreamManager/PerforceStreamManager.csproj
```

### Run Application
```bash
dotnet run --project PerforceStreamManager/PerforceStreamManager.csproj
```

### Test
```bash
# Run all tests
dotnet test PerforceStreamManager.Tests/PerforceStreamManager.Tests.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~SettingsServiceTests"

# Run single test method
dotnet test --filter "FullyQualifiedName~SettingsServiceTests.LoadSettings_WhenFileDoesNotExist_ReturnsDefaultSettings"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

### Clean
```bash
dotnet clean PerforceStreamManager.sln
```

## Project Structure
```
PerforceStreamManager/
├── Models/               # Data models (StreamNode, StreamRule, AppSettings, Snapshot)
├── Services/            # Business logic (P4Service, SnapshotService, SettingsService, LoggingService)
├── ViewModels/          # MVVM ViewModels with INotifyPropertyChanged
├── Views/               # XAML views and code-behind
├── App.xaml[.cs]        # Application entry point
└── MainWindow.xaml[.cs] # Main application window

PerforceStreamManager.Tests/
└── *Tests.cs            # NUnit test files
```

## Code Style Guidelines

### Naming Conventions
- **Classes, Methods, Properties, Events**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters, local variables**: `camelCase`
- **Boolean properties**: `IsConnected`, `HasChildren`, `CanExecute` prefix
- **Boolean method predicates**: `CanLoadStream`, `CanSaveSnapshot` prefix
- **Acronyms**: Treat as words (`P4Service`, not `P4SERVICE`)

### File Organization
- **Namespaces**: Use file-scoped namespaces for new files: `namespace PerforceStreamManager.Services;`
- **Using statements**: Order: System → Third-party (Perforce.P4) → Local, alphabetically within groups
- **One class per file**: Filename must match class name

### Types & Nullability
- **Nullable reference types enabled**: Use `?` for nullable types
- **Type inference**: Use `var` for obvious types, explicit types for clarity
- **Collections**: Use `ObservableCollection<T>` for data-bound collections in ViewModels
- **Async**: Return `Task` or `Task<T>`, use `async void` ONLY for event handlers

### MVVM Pattern
- **ViewModels**: Implement `INotifyPropertyChanged`, raise `PropertyChanged` in setters
- **Commands**: Use `RelayCommand` with `CanExecute` predicates
- **Data Binding**: Two-way binding with `UpdateSourceTrigger=PropertyChanged` for immediate updates
- **Code-Behind**: Minimal logic, only for event handlers that cannot be commands (e.g., `TreeView_SelectedItemChanged`)

### Error Handling
```csharp
// Pattern 1: Validate inputs with guard clauses
public void Connect(P4ConnectionSettings settings)
{
    if (settings == null)
        throw new ArgumentNullException(nameof(settings));
    
    // ... implementation
}

// Pattern 2: Wrap external exceptions with context
try
{
    // Perforce operation
}
catch (P4Exception ex)
{
    _loggingService.LogError(ex, "MethodName");
    throw new Exception($"Failed to perform operation: {ex.Message}", ex);
}

// Pattern 3: UI error handling
catch (Exception ex)
{
    MessageBox.Show($"Error: {ex.Message}", "Error", 
        MessageBoxButton.OK, MessageBoxImage.Error);
}

// Pattern 4: Guard methods
private void EnsureConnected()
{
    if (!IsConnected)
        throw new InvalidOperationException("Not connected to Perforce. Call Connect() first.");
}
```

### Async Patterns
```csharp
// Use Task.Run for CPU-bound work
await Task.Run(() => _p4Service.GetStreamHierarchy(streamPath));

// Update UI from background thread
Application.Current.Dispatcher.Invoke(() =>
{
    StreamHierarchy.Clear();
    foreach (var node in hierarchy)
        StreamHierarchy.Add(node);
});

// Progress window pattern for long operations
private async void RunWithProgressAsync(Func<Task> action, string message)
{
    var progressWindow = new Views.ProgressWindow(message);
    progressWindow.Show();
    try
    {
        await action();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Error", 
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        progressWindow.Close();
    }
}
```

### Logging
```csharp
// Log before operations
_loggingService.LogInfo($"Loading stream hierarchy for {streamPath}...");

// Log errors with context
_loggingService.LogError(ex, "Connect");

// Silent logging failures (logging should never break the app)
public void LogInfo(string message)
{
    try
    {
        File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n");
    }
    catch
    {
        // Silently fail
    }
}
```

### XAML Binding
```xml
<!-- Property binding with immediate updates -->
<TextBox Text="{Binding StreamPathInput, UpdateSourceTrigger=PropertyChanged}"/>

<!-- Command binding -->
<Button Content="Load" Command="{Binding LoadStreamCommand}"/>

<!-- ItemsSource with hierarchical template -->
<TreeView ItemsSource="{Binding StreamHierarchy}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <TextBlock Text="{Binding Name}"/>
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>

<!-- DataGrid with explicit columns -->
<DataGrid ItemsSource="{Binding DisplayedRules}" 
          AutoGenerateColumns="False" 
          IsReadOnly="True">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Type" Binding="{Binding RuleType}"/>
        <DataGridTextColumn Header="Path" Binding="{Binding Path}"/>
    </DataGrid.Columns>
</DataGrid>
```

## Testing Guidelines

### Test Structure
- **Framework**: NUnit with FsCheck for property-based testing
- **Naming**: `MethodName_Condition_ExpectedBehavior`
- **Attributes**: `[TestFixture]` for classes, `[Test]` for methods
- **Setup/Teardown**: Use `[SetUp]` and `[TearDown]` for test initialization and cleanup

### Test Pattern
```csharp
[Test]
public void SaveAndLoadSettings_RoundTrip_PreservesAllValues()
{
    // Arrange
    var originalSettings = new AppSettings { /* ... */ };
    
    // Act
    _settingsService.SaveSettings(originalSettings);
    AppSettings loadedSettings = _settingsService.LoadSettings();
    
    // Assert
    Assert.IsNotNull(loadedSettings);
    Assert.AreEqual(originalSettings.Connection.Server, loadedSettings.Connection.Server);
}

[Test]
public void SaveSettings_WithNullSettings_ThrowsArgumentNullException()
{
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => _settingsService.SaveSettings(null));
}
```

## Key Dependencies
- **P4API.NET** (2025.2.287.2434): Perforce operations
- **System.Text.Json** (10.0.1): Settings and snapshot serialization
- **NUnit** (3.14.0): Test framework
- **FsCheck** (3.3.2): Property-based testing

## Configuration
- **Settings**: `%LocalAppData%\PerforceStreamManager\settings.json`
- **Logs**: `%AppData%\PerforceStreamManager\application.log`
- **Snapshots**: Stored in depot at configured `HistoryStoragePath`

## Common Tasks

### Adding a New Service
1. Create class in `Services/` folder
2. Constructor inject `LoggingService` and other dependencies
3. Log operations and errors consistently
4. Throw meaningful exceptions with context

### Adding a New ViewModel
1. Inherit from `INotifyPropertyChanged`
2. Private fields with `_camelCase`, public properties with `PascalCase`
3. Raise `PropertyChanged` in property setters
4. Use `RelayCommand` for user actions
5. Constructor inject required services

### Adding a New View
1. Create XAML + code-behind in `Views/`
2. Set `DataContext` to ViewModel in constructor
3. Use data binding for all UI updates
4. Minimize code-behind logic

### Working with Perforce
- Always use `P4Service` wrapper, never call P4API.NET directly from UI
- Check `IsConnected` before operations or use `EnsureConnected()`
- Wrap P4 operations in try-catch with logging
- Use `Task.Run()` for P4 operations to avoid blocking UI

## Best Practices
- Never block UI thread with long-running operations
- Always log errors before throwing or displaying to user
- Use progress windows for operations > 1 second
- Validate all user inputs before processing
- Clean up resources in Dispose/finally blocks
- Use `CommandManager.InvalidateRequerySuggested()` to refresh command states
