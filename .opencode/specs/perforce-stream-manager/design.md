# Design Document: Perforce Stream Rule Manager

## Overview

The Perforce Stream Rule Manager is a WPF desktop application that helps developers visualize and manage ignore/remap path rules across hierarchical Perforce streams. The application addresses three key pain points: difficulty seeing cumulative rule effects across parent-child streams, lack of stream specification change history in Perforce, and error-prone manual path entry.

The application uses P4API.NET for all Perforce operations and stores manual snapshots as JSON files in the depot for team-shared history tracking. The UI provides a tree view of stream hierarchies, three viewing modes for rules (Local, Inherited, All), and a file browser for error-free path selection.

## Architecture

The application follows the MVVM (Model-View-ViewModel) pattern for clean separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                        View Layer                        │
│  (WPF XAML: MainWindow, Dialogs, UserControls)         │
└────────────────┬────────────────────────────────────────┘
                 │ Data Binding
┌────────────────▼────────────────────────────────────────┐
│                    ViewModel Layer                       │
│  (MainViewModel, RuleViewModel, HistoryViewModel)       │
└────────────────┬────────────────────────────────────────┘
                 │ Commands & Properties
┌────────────────▼────────────────────────────────────────┐
│                      Service Layer                       │
│  (P4Service, SnapshotService, SettingsService)         │
└────────────────┬────────────────────────────────────────┘
                 │ P4API.NET
┌────────────────▼────────────────────────────────────────┐
│                   Perforce Server                        │
│              (Streams, Depot, Files)                     │
└─────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

1. **MVVM Pattern**: Separates UI logic from business logic, enabling testability and maintainability
2. **Service Layer**: Encapsulates all Perforce operations and snapshot management
3. **JSON Storage**: Uses simple JSON format for snapshots, stored directly in depot for team access
4. **Synchronous Operations**: UI operations are synchronous with progress indicators (simpler than async for this use case)

## Components and Interfaces

### 1. View Layer (WPF XAML)

#### MainWindow
The primary application window containing:
- TreeView for stream hierarchy
- DataGrid for rule display with three view modes
- Toolbar with Add/Edit/Delete/Snapshot buttons
- Menu bar with Settings and History options

#### Dialogs
- **RuleDialog**: Add/edit rule with file browser integration
- **SettingsDialog**: Configure P4 connection and history settings
- **HistoryWindow**: Timeline view with snapshot comparison and restore
- **DepotBrowserDialog**: Navigate depot structure to select paths

### 2. ViewModel Layer

#### MainViewModel
```csharp
class MainViewModel : INotifyPropertyChanged
{
    // Properties
    ObservableCollection<StreamNode> StreamHierarchy { get; }
    StreamNode SelectedStream { get; set; }
    ObservableCollection<RuleViewModel> DisplayedRules { get; }
    RuleViewMode CurrentViewMode { get; set; } // Local, Inherited, All
    
    // Commands
    ICommand LoadStreamCommand { get; }
    ICommand AddRuleCommand { get; }
    ICommand EditRuleCommand { get; }
    ICommand DeleteRuleCommand { get; }
    ICommand CreateSnapshotCommand { get; }
    ICommand OpenHistoryCommand { get; }
    ICommand OpenSettingsCommand { get; }
    
    // Methods
    void LoadStreamHierarchy(string streamPath)
    void RefreshRuleDisplay()
    void ChangeViewMode(RuleViewMode mode)
}
```

#### RuleViewModel
```csharp
class RuleViewModel : INotifyPropertyChanged
{
    string RuleType { get; set; } // "ignore" or "remap"
    string Path { get; set; }
    string SourceStream { get; set; } // Which stream defined this rule
    bool IsInherited { get; set; }
    bool IsLocal { get; set; }
}
```

#### HistoryViewModel
```csharp
class HistoryViewModel : INotifyPropertyChanged
{
    ObservableCollection<SnapshotInfo> Snapshots { get; }
    SnapshotInfo SelectedSnapshot { get; set; }
    SnapshotInfo ComparisonSnapshot { get; set; }
    ObservableCollection<RuleDiff> DiffResults { get; }
    
    ICommand LoadHistoryCommand { get; }
    ICommand CompareSnapshotsCommand { get; }
    ICommand RestoreSnapshotCommand { get; }
}
```

### 3. Service Layer

#### P4Service
Handles all Perforce operations using P4API.NET:

```csharp
class P4Service
{
    // Connection Management
    void Connect(P4ConnectionSettings settings)
    void Disconnect()
    bool IsConnected { get; }
    
    // Stream Operations
    Stream GetStream(string streamPath)
    List<Stream> GetStreamHierarchy(string rootStreamPath)
    List<StreamRule> GetStreamRules(string streamPath)
    void UpdateStreamRules(string streamPath, List<StreamRule> rules)
    
    // Depot Operations
    List<DepotFile> GetDepotFiles(string depotPath)
    List<string> GetDepotDirectories(string depotPath)
    string ReadDepotFile(string depotPath)
    void WriteDepotFile(string depotPath, string content, string description)
}
```

#### SnapshotService
Manages snapshot creation, storage, and retrieval:

```csharp
class SnapshotService
{
    // Snapshot Operations
    Snapshot CreateSnapshot(string streamPath, List<StreamRule> rules)
    void SaveSnapshot(Snapshot snapshot, string historyPath)
    List<Snapshot> LoadHistory(string streamPath, string historyPath)
    
    // Comparison and Restore
    SnapshotDiff CompareSnapshots(Snapshot snapshot1, Snapshot snapshot2)
    void RestoreSnapshot(string streamPath, Snapshot snapshot)
    
    // Retention Management
    void ApplyRetentionPolicy(string streamPath, RetentionPolicy policy)
}
```

#### SettingsService
Manages application settings persistence:

```csharp
class SettingsService
{
    AppSettings LoadSettings()
    void SaveSettings(AppSettings settings)
}
```

### 4. Model Classes

#### StreamNode
```csharp
class StreamNode
{
    string Name { get; set; }
    string Path { get; set; }
    StreamNode Parent { get; set; }
    List<StreamNode> Children { get; set; }
    List<StreamRule> LocalRules { get; set; }
    List<StreamRule> InheritedRules { get; set; }
}
```

#### StreamRule
```csharp
class StreamRule
{
    string Type { get; set; } // "ignore" or "remap"
    string Path { get; set; }
    string RemapTarget { get; set; } // Only for remap rules
    string SourceStream { get; set; } // Stream that defined this rule
}
```

#### Snapshot
```csharp
class Snapshot
{
    string StreamPath { get; set; }
    DateTime Timestamp { get; set; }
    string CreatedBy { get; set; }
    List<StreamRule> Rules { get; set; }
    string Description { get; set; }
}
```

#### SnapshotDiff
```csharp
class SnapshotDiff
{
    List<StreamRule> AddedRules { get; set; }
    List<StreamRule> RemovedRules { get; set; }
    List<RuleChange> ModifiedRules { get; set; }
}

class RuleChange
{
    StreamRule OldRule { get; set; }
    StreamRule NewRule { get; set; }
}
```

#### AppSettings
```csharp
class AppSettings
{
    P4ConnectionSettings Connection { get; set; }
    string HistoryStoragePath { get; set; }
    RetentionPolicy Retention { get; set; }
}

class P4ConnectionSettings
{
    string Server { get; set; }
    string Port { get; set; }
    string User { get; set; }
}

class RetentionPolicy
{
    int MaxSnapshots { get; set; }
    int MaxAgeDays { get; set; }
}
```

## Data Models

### JSON Snapshot Format

Each stream has its own JSON history file stored in the depot at the configured history path. The file contains an array of snapshots:

```json
{
  "streamPath": "//depot/main/dev",
  "snapshots": [
    {
      "timestamp": "2026-01-07T10:30:00Z",
      "createdBy": "john.doe",
      "description": "Before refactoring",
      "rules": [
        {
          "type": "ignore",
          "path": "//depot/main/dev/temp/...",
          "sourceStream": "//depot/main/dev"
        },
        {
          "type": "remap",
          "path": "//depot/main/dev/lib/...",
          "remapTarget": "//depot/shared/lib/...",
          "sourceStream": "//depot/main"
        }
      ]
    }
  ]
}
```

### Stream Hierarchy Data Structure

The application builds an in-memory tree of StreamNode objects representing the parent-child relationships:

```
StreamNode: //depot/main
├── LocalRules: [ignore: temp/...]
├── Children:
    ├── StreamNode: //depot/main/dev
    │   ├── LocalRules: [ignore: build/...]
    │   ├── InheritedRules: [ignore: temp/...] (from //depot/main)
    │   └── Children:
    │       └── StreamNode: //depot/main/dev/feature-x
    │           ├── LocalRules: []
    │           └── InheritedRules: [ignore: temp/..., ignore: build/...]
    └── StreamNode: //depot/main/release
        ├── LocalRules: [remap: lib/... -> //depot/shared/lib/...]
        └── InheritedRules: [ignore: temp/...]
```

### Rule Inheritance Algorithm

When displaying rules in "All" or "Inherited" mode, the application walks up the parent chain:

```
function GetAllRules(stream):
    allRules = []
    currentStream = stream
    
    while currentStream != null:
        for rule in currentStream.LocalRules:
            rule.SourceStream = currentStream.Path
            allRules.add(rule)
        currentStream = currentStream.Parent
    
    return allRules

function GetInheritedRules(stream):
    inheritedRules = []
    currentStream = stream.Parent
    
    while currentStream != null:
        for rule in currentStream.LocalRules:
            rule.SourceStream = currentStream.Path
            inheritedRules.add(rule)
        currentStream = currentStream.Parent
    
    return inheritedRules
```

## Error Handling

### Perforce Connection Errors
- **Connection Failure**: Display error dialog with connection details, allow retry or settings modification
- **Authentication Failure**: Prompt for credentials, update settings if successful
- **Network Timeout**: Show timeout message, offer retry option

### Stream Operation Errors
- **Stream Not Found**: Display error message, refresh stream list
- **Permission Denied**: Show permission error, suggest contacting admin
- **Stream Locked**: Inform user stream is locked by another user, show lock details

### Snapshot Operation Errors
- **History File Not Found**: Create new history file for stream
- **JSON Parse Error**: Log error, show corrupted file message, offer to create new history
- **Depot Write Failure**: Show error, suggest checking permissions and disk space
- **Retention Policy Failure**: Log warning, continue operation

### UI Error Handling
- **Invalid Path Entry**: Validate paths before submission, show validation errors
- **Empty Rule Submission**: Prevent submission, highlight required fields
- **Concurrent Modification**: Detect if stream changed since load, prompt to refresh

### Error Logging
All errors are logged to a local file with:
- Timestamp
- Error type and message
- Stack trace
- User and connection context

## Testing Strategy

### Unit Tests
The application will use NUnit for unit testing with the following focus areas:

**Service Layer Tests**:
- P4Service: Mock P4API.NET calls, test connection handling, stream retrieval, rule updates
- SnapshotService: Test JSON serialization/deserialization, snapshot comparison logic, retention policy application
- SettingsService: Test settings persistence and retrieval

**Model Tests**:
- StreamNode: Test hierarchy building, rule inheritance algorithm
- SnapshotDiff: Test diff calculation logic

**ViewModel Tests**:
- MainViewModel: Test command execution, view mode switching, rule filtering
- HistoryViewModel: Test snapshot loading, comparison, restore operations

### Property-Based Tests
Property-based tests will verify universal correctness properties using FsCheck (C# property testing library). Each property test will run a minimum of 100 iterations with randomized inputs.


## Correctness Properties

A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.

The following properties will be validated using property-based testing with FsCheck. Each property will be tested with a minimum of 100 randomized inputs to ensure correctness across the input space.

### Property 1: Stream Hierarchy Loading
*For any* valid stream path, loading the stream hierarchy should produce a tree structure where each node's children list matches the actual child streams in Perforce.
**Validates: Requirements 1.1**

### Property 2: Local View Filtering
*For any* stream with both local and inherited rules, the Local view mode should display exactly the rules defined in that stream and no inherited rules.
**Validates: Requirements 2.2**

### Property 3: Inherited View Filtering
*For any* stream with parent streams, the Inherited view mode should display exactly the rules from parent streams and no local rules.
**Validates: Requirements 2.3**

### Property 4: All View Completeness
*For any* stream, the All view mode should display exactly the union of local rules and inherited rules, with no duplicates or omissions.
**Validates: Requirements 2.4**

### Property 5: Rule Source Tracking
*For any* rule displayed in Inherited or All view mode, the rule's data model should include the source stream path identifying which stream defined that rule.
**Validates: Requirements 2.5**

### Property 6: Rule Addition
*For any* valid rule and stream, adding the rule to the stream should result in the stream's local rules containing that rule.
**Validates: Requirements 3.2**

### Property 7: Rule Modification
*For any* existing rule in a stream, editing the rule should result in the stream containing the modified rule and not the original rule.
**Validates: Requirements 3.4**

### Property 8: Rule Deletion
*For any* rule in a stream, deleting the rule should result in the stream no longer containing that rule in its local rules.
**Validates: Requirements 3.5**

### Property 9: Path Selection Propagation
*For any* depot path selected in the file browser, the path field should be populated with exactly that depot path string.
**Validates: Requirements 4.3**

### Property 10: Snapshot Completeness
*For any* stream, creating a snapshot should capture all current local and inherited rules for that stream.
**Validates: Requirements 5.1**

### Property 11: Snapshot Serialization Round-Trip
*For any* valid snapshot, serializing it to JSON and then deserializing should produce an equivalent snapshot with all fields preserved (timestamp, stream name, created by, description, and all rule details).
**Validates: Requirements 5.2, 6.3**

### Property 12: Snapshot Storage Location
*For any* snapshot saved to the depot, the JSON file should exist at the configured history storage path.
**Validates: Requirements 5.3**

### Property 13: One History File Per Stream
*For any* set of streams, each stream should have exactly one history file in the depot, regardless of how many snapshots are created.
**Validates: Requirements 5.5**

### Property 14: Snapshot Append Preserves History
*For any* existing history file with N snapshots, appending a new snapshot should result in the history file containing N+1 snapshots with all original snapshots preserved.
**Validates: Requirements 5.6**

### Property 15: Timeline Chronological Ordering
*For any* stream with multiple snapshots, the history viewer should display snapshots in chronological order sorted by timestamp (oldest to newest or newest to oldest consistently).
**Validates: Requirements 7.1**

### Property 16: Snapshot Selection Display
*For any* snapshot selected in the timeline, the displayed rules should exactly match the rules stored in that snapshot.
**Validates: Requirements 7.3**

### Property 17: Snapshot Diff Completeness
*For any* two snapshots, the diff calculation should correctly identify all added rules (in snapshot2 but not snapshot1), all removed rules (in snapshot1 but not snapshot2), and all modified rules (same path but different properties).
**Validates: Requirements 8.2, 8.3, 8.4**

### Property 18: Snapshot Restore
*For any* snapshot, restoring it to a stream should result in the stream's rules exactly matching the snapshot's rules.
**Validates: Requirements 9.1, 9.2**

### Property 19: Settings Persistence Round-Trip
*For any* valid application settings (connection parameters, history path, retention policy), saving the settings and then loading them should produce equivalent settings with all values preserved.
**Validates: Requirements 10.2, 10.5**


## Implementation Notes

### P4API.NET Integration
The application will use P4API.NET NuGet package for all Perforce operations. Key classes to use:
- `P4Server`: Connection management
- `P4Command`: Execute Perforce commands
- `Stream`: Stream object representation
- `FileSpec`: File and depot path specifications

### WPF MVVM Implementation
- Use `INotifyPropertyChanged` for property change notifications
- Use `ICommand` interface for command binding
- Use `ObservableCollection<T>` for data-bound collections
- Consider using a lightweight MVVM framework like Prism or MVVMLight for command helpers

### JSON Serialization
Use `System.Text.Json` or `Newtonsoft.Json` for snapshot serialization with proper date/time formatting and null handling.

### Settings Storage
Store application settings in user's AppData folder using JSON format for easy editing and portability.

### Performance Considerations
- Cache stream hierarchy after initial load
- Lazy-load rule details only when stream is selected
- Use background workers for long-running Perforce operations with progress reporting
- Limit history file size through retention policy

### Security Considerations
- Store Perforce credentials securely (consider Windows Credential Manager)
- Validate all user inputs before Perforce operations
- Sanitize depot paths to prevent injection attacks
- Log all rule modifications for audit trail
