# Perforce Stream Manager - Subagents

This directory contains specialized subagent configurations for the Perforce Stream Manager project. Each subagent is an expert in a specific domain and handles targeted development tasks.

## Available Subagents

### 1. **wpf-ui-designer** (High Priority)
**File**: `wpf-ui-designer.md`

**Purpose**: WPF UI design and XAML implementation specialist

**Expertise**:
- XAML markup and layout design
- Data binding (OneWay, TwoWay, UpdateSourceTrigger)
- WPF controls (TreeView, DataGrid, dialogs)
- HierarchicalDataTemplate for tree structures
- INotifyPropertyChanged implementation
- Visual design and styling

**Use When**:
- Creating or modifying XAML files
- Implementing data binding
- Designing dialogs and windows
- Working with TreeView hierarchies
- Styling WPF controls
- Creating code-behind for views

---

### 2. **p4-integration-specialist** (High Priority)
**File**: `p4-integration-specialist.md`

**Purpose**: Perforce P4API.NET integration expert

**Expertise**:
- P4API.NET library operations
- Stream hierarchy management
- Stream rule parsing (ignore/remap)
- Depot file operations (read/write/submit)
- Connection management and authentication
- P4Exception handling

**Use When**:
- Implementing P4Service methods
- Working with Perforce streams
- Reading/writing depot files
- Building stream hierarchies
- Handling Perforce errors
- Managing P4 connections

---

### 3. **mvvm-architect** (Medium Priority)
**File**: `mvvm-architect.md`

**Purpose**: MVVM pattern implementation and architecture specialist

**Expertise**:
- ViewModelBase implementation
- RelayCommand pattern
- INotifyPropertyChanged
- ObservableCollection bindings
- Command CanExecute logic
- Separation of concerns

**Use When**:
- Creating ViewModels
- Implementing commands
- Setting up property change notifications
- Coordinating View-ViewModel relationships
- Implementing async operations in ViewModels
- Ensuring MVVM best practices

---

### 4. **test-generator** (Medium Priority)
**File**: `test-generator.md`

**Purpose**: Comprehensive test creation specialist (NUnit + FsCheck)

**Expertise**:
- NUnit test framework
- FsCheck property-based testing
- Test organization and naming
- Mocking P4API.NET
- Test coverage analysis
- All 19 correctness properties

**Use When**:
- Writing unit tests for services
- Creating property-based tests
- Implementing test fixtures
- Creating mock objects
- Validating correctness properties
- Achieving test coverage goals

---

### 5. **json-data-manager** (Low Priority)
**File**: `json-data-manager.md`

**Purpose**: JSON serialization and data persistence specialist

**Expertise**:
- System.Text.Json serialization
- SnapshotService implementation
- SettingsService implementation
- Round-trip validation
- DateTime handling
- Data migration support

**Use When**:
- Implementing snapshot save/load
- Creating settings persistence
- Working with JSON serialization
- Implementing retention policies
- Handling data versioning
- Ensuring round-trip correctness

---

### 6. **error-handler** (Low Priority)
**File**: `error-handler.md`

**Purpose**: Error handling, logging, and audit trail specialist

**Expertise**:
- LoggingService implementation
- Exception handling patterns
- Custom exception types
- User-friendly error messages
- Audit trail logging
- Global exception handling

**Use When**:
- Implementing error handling
- Creating logging infrastructure
- Designing error messages
- Setting up audit trails
- Handling P4Exceptions
- Creating custom exception types

---

### 7. **debugger** (Critical Priority)
**File**: `debugger.md`

**Purpose**: WPF debugging, diagnostics, and troubleshooting specialist

**Expertise**:
- WPF binding diagnostics (PresentationTraceSources)
- MVVM debugging (PropertyChanged, commands)
- P4Exception analysis and categorization
- Threading/async debugging (UI freezes, deadlocks)
- Memory leak detection and profiling
- Performance bottleneck identification
- Visual tree inspection (Snoop, Live Visual Tree)

**Use When**:
- Debugging binding failures ("UI not updating")
- Diagnosing P4 connection errors
- Fixing UI freezes or performance issues
- Investigating memory leaks
- Troubleshooting command CanExecute issues
- Analyzing PropertyChanged event problems
- Profiling long-running operations
- Creating diagnostic/instrumentation code

---

## How to Use Subagents

### Invoking a Subagent

When you need specialized help, reference the appropriate subagent in your request:

```
@wpf-ui-designer Please create the MainWindow.xaml with TreeView and DataGrid layout
```

```
@p4-integration-specialist Implement GetStreamHierarchy method in P4Service
```

```
@test-generator Create property-based tests for snapshot serialization round-trip
```

### Subagent Selection Guide

| Task | Primary Subagent | Supporting Subagents |
|------|------------------|---------------------|
| Create new view | wpf-ui-designer | mvvm-architect |
| Implement ViewModel | mvvm-architect | wpf-ui-designer |
| P4 operations | p4-integration-specialist | error-handler |
| Write tests | test-generator | - |
| JSON operations | json-data-manager | error-handler |
| Error handling | error-handler | All others |
| Debug issues | debugger | All others |
| Performance tuning | debugger | mvvm-architect, p4-integration-specialist |
| UI not updating | debugger | wpf-ui-designer, mvvm-architect |
| Full feature | Multiple | Coordinate as needed |

### Implementation Priority

**Phase 1 - Foundation** (Start Here):
1. error-handler - Get logging and error handling infrastructure
2. p4-integration-specialist - Implement core P4Service
3. json-data-manager - Implement settings and snapshot services

**Phase 2 - UI & Architecture**:
4. mvvm-architect - Create ViewModels and commands
5. wpf-ui-designer - Build XAML views and dialogs

**Phase 3 - Quality**:
6. test-generator - Comprehensive test coverage

## Subagent Coordination

For complex features that span multiple domains, coordinate subagents:

**Example: Implementing "Load Stream Hierarchy" Feature**

1. **p4-integration-specialist**: Implement `P4Service.GetStreamHierarchy()`
2. **error-handler**: Add error handling and logging
3. **mvvm-architect**: Create `LoadStreamCommand` in `MainViewModel`
4. **wpf-ui-designer**: Add TreeView to MainWindow.xaml
5. **test-generator**: Create unit and property-based tests

## File Organization

```
.opencode/subagents/
├── README.md                      # This file
├── wpf-ui-designer.md             # WPF/XAML specialist
├── p4-integration-specialist.md   # P4API.NET specialist
├── mvvm-architect.md              # MVVM pattern specialist
├── test-generator.md              # Testing specialist
├── json-data-manager.md           # JSON/data specialist
├── error-handler.md               # Error handling specialist
└── debugger.md                    # Debugging specialist
```

## Best Practices

1. **Single Responsibility**: Each subagent focuses on its domain
2. **Clear Boundaries**: Don't mix concerns (e.g., P4 operations in ViewModels)
3. **Consistent Patterns**: Follow patterns established by each subagent
4. **Complete Implementation**: Each subagent provides full implementation examples
5. **Reference Documentation**: All subagents reference requirements.md and design.md

## Common Workflows

### Adding a New Feature

1. Review requirements in `.opencode/specs/perforce-stream-manager/requirements.md`
2. Check design in `.opencode/specs/perforce-stream-manager/design.md`
3. Identify required subagents
4. Implement in layers (Service → ViewModel → View)
5. Add error handling throughout
6. Write comprehensive tests

### Fixing a Bug

1. Identify the layer where the bug exists
2. Use appropriate subagent for that layer
3. Add test to prevent regression (test-generator)
4. Implement fix following subagent patterns
5. Update error handling if needed

### Refactoring

1. Ensure tests exist (test-generator)
2. Apply refactoring using appropriate subagent
3. Verify tests still pass
4. Update documentation if patterns change

## Reference Documentation

All subagents reference these core documents:

- **Requirements**: `.opencode/specs/perforce-stream-manager/requirements.md`
- **Design**: `.opencode/specs/perforce-stream-manager/design.md`
- **Agent Guidelines**: `AGENTS.md` (root)

## Success Criteria

You'll know the subagents are working well when:

- Each domain has clear ownership
- Code follows consistent patterns
- Error handling is comprehensive
- Tests cover all correctness properties
- MVVM separation is maintained
- P4 operations are properly encapsulated
- JSON serialization round-trips correctly

---

**Note**: These subagents are designed to work together. Don't hesitate to involve multiple subagents for complex features, but always maintain clear separation of concerns.
