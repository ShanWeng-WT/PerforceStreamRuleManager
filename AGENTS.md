# Perforce Stream Rule Manager - Agent Development Guide

## Overview
WPF desktop application for managing Perforce stream hierarchies using P4API.NET. Built with .NET 8.0, MVVM pattern, and NUnit for testing.

## Communication Style Preferences
- **Be concise**: Provide clear, direct answers without unnecessary elaboration
- **No unsolicited guidance**: Do not provide "What Needs To Be Done Next", "Manual Testing Checklists", or "Context for Next Session" unless explicitly requested
- **Answer what's asked**: If the user asks "What did we do?", summarize completed work only - don't add future recommendations
- **Trust the user**: The user will ask for help when needed; avoid being overly prescriptive or verbose

## Documentation
- **Requirements**: `.opencode/specs/perforce-stream-manager/requirements.md`
- **Design Specs**: `.opencode/specs/perforce-stream-manager/design.md`

## Build & Test Commands

```bash
# Build
dotnet build PerforceStreamManager.sln

# Run
dotnet run --project PerforceStreamManager/PerforceStreamManager.csproj

# Test
dotnet test PerforceStreamManager.Tests/PerforceStreamManager.Tests.csproj

# Test with filter
dotnet test --filter "FullyQualifiedName~SettingsServiceTests"
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
- **Acronyms**: Treat as words (`P4Service`, not `P4SERVICE`)

### File Organization
- **Namespaces**: Use file-scoped namespaces: `namespace PerforceStreamManager.Services;`
- **Using statements**: Order: System → Third-party (Perforce.P4) → Local
- **One class per file**: Filename must match class name

### Types & Nullability
- **Nullable reference types enabled**: Use `?` for nullable types
- **Collections**: Use `ObservableCollection<T>` for data-bound collections in ViewModels
- **Async**: Return `Task` or `Task<T>`, use `async void` ONLY for event handlers

### MVVM Pattern
- **ViewModels**: Implement `INotifyPropertyChanged`, raise `PropertyChanged` in setters
- **Commands**: Use `RelayCommand` with `CanExecute` predicates
- **Data Binding**: Two-way binding with `UpdateSourceTrigger=PropertyChanged`
- **Code-Behind**: Minimal logic, only for events that cannot be commands

### Error Handling
```csharp
// Guard clauses
if (settings == null) throw new ArgumentNullException(nameof(settings));

// Wrap P4 exceptions
catch (P4Exception ex)
{
    _loggingService.LogError(ex, "MethodName");
    throw new Exception($"Failed: {ex.Message}", ex);
}

// Guard methods
private void EnsureConnected()
{
    if (!IsConnected)
        throw new InvalidOperationException("Not connected to Perforce.");
}
```

### Async Patterns
```csharp
// Use Task.Run for P4 operations
var result = await Task.Run(() => _p4Service.GetStreamHierarchy(streamPath));

// Update UI from background thread
Application.Current.Dispatcher.Invoke(() => { /* update UI */ });
```

## Testing Guidelines
- **Framework**: NUnit with FsCheck for property-based testing
- **Naming**: `MethodName_Condition_ExpectedBehavior`
- **Pattern**: Arrange-Act-Assert

## Key Dependencies
- **P4API.NET** (2025.2.287.2434): Perforce operations
- **System.Text.Json** (10.0.1): Settings and snapshot serialization
- **NUnit** (3.14.0): Test framework
- **FsCheck** (3.3.2): Property-based testing

## Configuration
- **Settings**: `%LocalAppData%\PerforceStreamManager\settings.json`
- **Logs**: `%AppData%\PerforceStreamManager\application.log`
- **Snapshots**: Stored in depot at configured `HistoryStoragePath`

## Best Practices
- Never block UI thread with long-running operations
- Always log errors before throwing or displaying to user
- Use progress windows for operations > 1 second
- Validate all user inputs before processing
- Use `CommandManager.InvalidateRequerySuggested()` to refresh command states
