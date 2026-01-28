# Requirements Document

## Introduction

This document specifies the requirements for a WPF C# desktop application that manages Perforce stream hierarchies. The application visualizes and tracks ignore/remap path rules across parent-child streams, addresses the difficulty of seeing cumulative rule effects, provides manual snapshot history tracking, and reduces path entry errors through a file browser interface.

## Glossary

- **Stream_Rule_Manager**: The WPF desktop application for managing Perforce stream rule hierarchies
- **Stream**: A Perforce stream with parent-child relationships in a hierarchy
- **Rule**: An ignore or remap path specification in a stream configuration
- **Snapshot**: A JSON file stored in the depot containing the state of stream rules at a point in time
- **Depot**: The Perforce version control repository
- **P4API**: The Perforce .NET API library used for Perforce operations
- **Tree_View**: The hierarchical visual display of stream parent-child relationships
- **History_File**: A JSON file in the depot storing snapshots for a specific stream

## Requirements

### Requirement 1: Stream Hierarchy Visualization

**User Story:** As a developer, I want to view stream hierarchies as a tree structure, so that I can understand parent-child relationships at a glance.

#### Acceptance Criteria

1. WHEN the application loads a stream, THE Stream_Rule_Manager SHALL display the stream hierarchy as a tree view
2. WHEN displaying the tree view, THE Stream_Rule_Manager SHALL show parent-child relationships with visual indentation
3. WHEN a user selects a stream in the tree, THE Stream_Rule_Manager SHALL highlight the selected stream
4. THE Stream_Rule_Manager SHALL load stream hierarchy data using P4API.NET

### Requirement 2: Rule Display and Categorization

**User Story:** As a developer, I want to see rules in three separate views (Local, Inherited, All), so that I can understand which rules come from which streams.

#### Acceptance Criteria

1. WHEN displaying rules for a selected stream, THE Stream_Rule_Manager SHALL provide three view modes: Local, Inherited, and All
2. WHEN the Local view is active, THE Stream_Rule_Manager SHALL display only rules defined in the currently selected stream
3. WHEN the Inherited view is active, THE Stream_Rule_Manager SHALL display only rules inherited from parent streams
4. WHEN the All view is active, THE Stream_Rule_Manager SHALL display both local and inherited rules
5. WHEN displaying inherited or all rules, THE Stream_Rule_Manager SHALL show a visual indicator identifying which stream added each rule
6. THE Stream_Rule_Manager SHALL retrieve rule information using P4API.NET

### Requirement 3: Rule Management Operations

**User Story:** As a developer, I want to add, edit, and delete rules one at a time, so that I can maintain stream configurations accurately.

#### Acceptance Criteria

1. WHEN a user initiates an add rule operation, THE Stream_Rule_Manager SHALL display a dialog for entering rule details
2. WHEN a user submits a new rule, THE Stream_Rule_Manager SHALL add the rule to the selected stream using P4API.NET
3. WHEN a user initiates an edit rule operation, THE Stream_Rule_Manager SHALL display a dialog pre-populated with existing rule details
4. WHEN a user submits an edited rule, THE Stream_Rule_Manager SHALL update the rule in the selected stream using P4API.NET
5. WHEN a user initiates a delete rule operation, THE Stream_Rule_Manager SHALL remove the rule from the selected stream using P4API.NET
6. THE Stream_Rule_Manager SHALL apply rule changes to the stream specification through P4API.NET

### Requirement 4: File Browser for Path Selection

**User Story:** As a developer, I want to browse the depot structure and select paths visually, so that I can avoid typos when entering paths.

#### Acceptance Criteria

1. WHEN a user needs to specify a path for a rule, THE Stream_Rule_Manager SHALL provide a file browser dialog
2. WHEN the file browser opens, THE Stream_Rule_Manager SHALL display the depot directory structure using P4API.NET
3. WHEN a user selects a path in the file browser, THE Stream_Rule_Manager SHALL populate the path field with the selected depot path
4. THE Stream_Rule_Manager SHALL allow navigation through depot folders in the file browser

### Requirement 5: Manual Snapshot Creation

**User Story:** As a developer, I want to manually create snapshots of stream rule configurations, so that I can track changes over time with my team.

#### Acceptance Criteria

1. WHEN a user initiates a snapshot operation, THE Stream_Rule_Manager SHALL capture the current state of all rules for the entire stream hierarchy
2. WHEN creating a snapshot, THE Stream_Rule_Manager SHALL serialize the rule state as JSON with hierarchical structure (mapping stream paths to their rules)
3. WHEN saving a snapshot, THE Stream_Rule_Manager SHALL store the JSON file in the configured depot history path
4. WHEN saving a snapshot, THE Stream_Rule_Manager SHALL present a dialog allowing the user to choose between immediately submitting the snapshot or leaving it pending in the workspace
5. WHEN saving a snapshot, THE Stream_Rule_Manager SHALL commit the JSON file to the depot using P4API.NET
6. THE Stream_Rule_Manager SHALL create one history file per stream in the depot
7. WHEN appending to an existing history file, THE Stream_Rule_Manager SHALL maintain P4 versioning to track snapshot history

### Requirement 6: Snapshot History Storage

**User Story:** As a developer, I want snapshots stored as JSON files in the depot, so that my team can access shared history.

#### Acceptance Criteria

1. THE Stream_Rule_Manager SHALL store snapshots as JSON files in the depot
2. WHEN storing snapshots, THE Stream_Rule_Manager SHALL use one JSON file per stream with hierarchical format mapping stream paths to their rules
3. WHEN encoding snapshots, THE Stream_Rule_Manager SHALL include timestamp, hierarchical stream-to-rules mapping, and rule details in the JSON structure
4. WHEN storing snapshots, THE Stream_Rule_Manager SHALL leverage P4 file versioning to maintain complete history (rather than appending to a single JSON file)
5. THE Stream_Rule_Manager SHALL commit history files to the depot using P4API.NET
6. WHEN retrieving history, THE Stream_Rule_Manager SHALL parse JSON files from the depot using P4API.NET
7. WHEN loading legacy snapshots, THE Stream_Rule_Manager SHALL support the previous flat rule list format for backward compatibility

### Requirement 7: History Viewer with Timeline

**User Story:** As a developer, I want to view snapshot history on a timeline, so that I can see when changes were made.

#### Acceptance Criteria

1. WHEN a user opens the history viewer for a stream, THE Stream_Rule_Manager SHALL display all snapshots in chronological order
2. WHEN displaying the timeline, THE Stream_Rule_Manager SHALL show the timestamp for each snapshot
3. WHEN a user selects a snapshot in the timeline, THE Stream_Rule_Manager SHALL display the rule state from that snapshot
4. THE Stream_Rule_Manager SHALL load snapshot history from the stream's JSON history file in the depot

### Requirement 8: Change Tracking

**User Story:** As a developer, I want to track changes to rules before saving, so that I can review what will be submitted to Perforce.

#### Acceptance Criteria

1. WHEN a user makes changes to rules in the stream hierarchy, THE Stream_Rule_Manager SHALL track which rules were added or deleted
2. WHEN displaying pending changes, THE Stream_Rule_Manager SHALL highlight rules that were added since the last load
3. WHEN displaying pending changes, THE Stream_Rule_Manager SHALL highlight rules that were deleted since the last load
4. WHEN closing the application with unsaved changes, THE Stream_Rule_Manager SHALL display the pending changes dialog showing which streams and rules would be affected
5. WHEN restoring from a snapshot, THE Stream_Rule_Manager SHALL restore all rules for the entire stream hierarchy and show the changes that will be applied

### Requirement 9: Snapshot Restore

**User Story:** As a developer, I want to restore a stream hierarchy to a previous snapshot state, so that I can revert unwanted changes.

#### Acceptance Criteria

1. WHEN a user initiates a restore operation for a snapshot, THE Stream_Rule_Manager SHALL display a dialog showing available revisions with timestamps, dates, users, and descriptions
2. WHEN a user selects a revision to restore, THE Stream_Rule_Manager SHALL load the rule state from that snapshot
3. WHEN restoring a snapshot, THE Stream_Rule_Manager SHALL apply the snapshot's rules to all streams in the current hierarchy using P4API.NET
4. WHEN restoring a snapshot, THE Stream_Rule_Manager SHALL replace the current stream rules with the snapshot rules
5. WHEN a restore operation completes, THE Stream_Rule_Manager SHALL update all stream specifications in Perforce using P4API.NET

### Requirement 10: Application Settings

**User Story:** As a developer, I want to configure P4 connection settings, history storage path, and retention policy, so that the application works with my Perforce environment.

#### Acceptance Criteria

1. THE Stream_Rule_Manager SHALL provide a settings interface for configuration
2. WHEN configuring settings, THE Stream_Rule_Manager SHALL allow specification of Perforce connection parameters (server, port, user, workspace)
3. WHEN configuring settings, THE Stream_Rule_Manager SHALL allow specification of the depot path for history file storage
4. WHEN configuring settings, THE Stream_Rule_Manager SHALL allow specification of a retention policy for snapshots
5. WHEN settings are saved, THE Stream_Rule_Manager SHALL persist the configuration for future application sessions
6. WHEN the application starts, THE Stream_Rule_Manager SHALL load saved settings and establish a Perforce connection using P4API.NET

### Requirement 11: Perforce API Integration

**User Story:** As a system architect, I want all Perforce operations to use P4API.NET, so that the application integrates reliably with Perforce.

#### Acceptance Criteria

1. THE Stream_Rule_Manager SHALL use P4API.NET for all Perforce server communication
2. WHEN retrieving stream information, THE Stream_Rule_Manager SHALL use P4API.NET commands
3. WHEN modifying stream specifications, THE Stream_Rule_Manager SHALL use P4API.NET commands
4. WHEN reading or writing depot files, THE Stream_Rule_Manager SHALL use P4API.NET commands
5. WHEN establishing connections, THE Stream_Rule_Manager SHALL use P4API.NET connection management

### Requirement 12: WPF User Interface

**User Story:** As a developer, I want a responsive WPF desktop interface, so that I can efficiently manage stream rules.

#### Acceptance Criteria

1. THE Stream_Rule_Manager SHALL be implemented as a WPF application using C#
2. WHEN the user interacts with UI elements, THE Stream_Rule_Manager SHALL provide immediate visual feedback
3. THE Stream_Rule_Manager SHALL use WPF controls for tree views, dialogs, and data display
4. WHEN performing long-running operations, THE Stream_Rule_Manager SHALL display progress indicators
5. THE Stream_Rule_Manager SHALL follow WPF MVVM pattern for UI implementation

---

## Additional Requirements (Beyond Base Scope)

### Requirement 13: Workspace Auto-Detection

**User Story:** As a developer, I want the application to automatically detect the appropriate Perforce workspace for a stream, so that I don't need to manually manage workspace context.

#### Acceptance Criteria

1. WHEN a user saves changes to streams, THE Stream_Rule_Manager SHALL automatically detect an appropriate workspace for the stream using P4API.NET
2. WHEN multiple workspaces are available, THE Stream_Rule_Manager SHALL select the most appropriate workspace for the operation
3. WHEN no suitable workspace exists, THE Stream_Rule_Manager SHALL fail gracefully with an error message

### Requirement 14: Application Context Persistence

**User Story:** As a developer, I want the application to remember my last used stream and settings, so that I don't need to reconfigure on each session.

#### Acceptance Criteria

1. WHEN the application closes, THE Stream_Rule_Manager SHALL save the currently loaded stream path to settings
2. WHEN the application starts, THE Stream_Rule_Manager SHALL load the last used stream path from settings
3. WHEN the last used stream is available, THE Stream_Rule_Manager SHALL automatically load it (subject to user configuration)

### Requirement 15: Snapshot Submission Control

**User Story:** As a developer, I want to control whether a snapshot is immediately submitted or left pending in my workspace, so that I can batch multiple changes together.

#### Acceptance Criteria

1. WHEN a user saves a snapshot, THE Stream_Rule_Manager SHALL display a dialog with submission options
2. THE Stream_Rule_Manager SHALL provide a checkbox option to immediately submit the snapshot to the depot
3. WHEN the immediate submission option is unchecked, THE Stream_Rule_Manager SHALL leave the snapshot file in the user's pending changelist
4. THE Stream_Rule_Manager SHALL default to immediate submission for safety and consistency
