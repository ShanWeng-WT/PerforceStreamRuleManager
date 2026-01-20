# Implementation Plan: Perforce Stream Rule Manager

## Overview

This implementation plan breaks down the Perforce Stream Rule Manager into discrete coding tasks following the MVVM architecture. The plan progresses from foundational models and services through to UI implementation, with property-based tests integrated throughout to validate correctness properties.

## Tasks

- [x] 1. Set up project structure and dependencies
  - Create WPF C# project with .NET Framework or .NET 6+
  - Add NuGet packages: P4API.NET, FsCheck, NUnit, System.Text.Json
  - Set up project folders: Models, ViewModels, Views, Services, Tests
  - Configure test project with references to main project
  - _Requirements: 11.1, 12.1_

- [x] 2. Implement core model classes
  - [x] 2.1 Create StreamRule, StreamNode, Snapshot, SnapshotDiff, and AppSettings model classes
    - Define all properties as specified in design document
    - Implement proper equality comparison for StreamRule
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 5.1, 6.3, 10.2_
  
  - [ ]* 2.2 Write property test for StreamRule equality
    - **Property: Rule Equality**
    - **Validates: Requirements 2.1**
  
  - [x] 2.3 Implement rule inheritance algorithm in StreamNode
    - Implement GetAllRules() method
    - Implement GetInheritedRules() method
    - Implement GetLocalRules() method
    - _Requirements: 2.2, 2.3, 2.4_
  
  - [ ]* 2.4 Write property tests for rule inheritance
    - **Property 2: Local View Filtering**
    - **Property 3: Inherited View Filtering**
    - **Property 4: All View Completeness**
    - **Validates: Requirements 2.2, 2.3, 2.4**

- [x] 3. Implement SettingsService
  - [x] 3.1 Create SettingsService class with LoadSettings and SaveSettings methods
    - Implement JSON serialization/deserialization
    - Store settings in AppData folder
    - Handle missing settings file (create defaults)
    - _Requirements: 10.5_
  
  - [ ]* 3.2 Write property test for settings persistence
    - **Property 19: Settings Persistence Round-Trip**
    - **Validates: Requirements 10.2, 10.5**

- [x] 4. Implement P4Service for Perforce operations
  - [x] 4.1 Create P4Service class with connection management
    - Implement Connect, Disconnect, IsConnected methods
    - Handle connection errors and authentication
    - _Requirements: 11.1, 11.5_
  
  - [x] 4.2 Implement stream operations in P4Service
    - Implement GetStream method
    - Implement GetStreamHierarchy method
    - Implement GetStreamRules method
    - Implement UpdateStreamRules method
    - _Requirements: 1.1, 2.6, 11.2, 11.3_
  
  - [ ]* 4.3 Write property tests for stream operations
    - **Property 1: Stream Hierarchy Loading**
    - **Property 6: Rule Addition**
    - **Property 7: Rule Modification**
    - **Property 8: Rule Deletion**
    - **Validates: Requirements 1.1, 3.2, 3.4, 3.5**
  
  - [x] 4.4 Implement depot file operations in P4Service
    - Implement GetDepotFiles method
    - Implement GetDepotDirectories method
    - Implement ReadDepotFile method
    - Implement WriteDepotFile method
    - _Requirements: 4.2, 5.4, 6.4, 11.4_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement SnapshotService
  - [x] 6.1 Create SnapshotService class with snapshot creation
    - Implement CreateSnapshot method
    - Capture all local and inherited rules
    - Include timestamp, user, and description
    - _Requirements: 5.1, 6.3_
  
  - [ ]* 6.2 Write property test for snapshot completeness
    - **Property 10: Snapshot Completeness**
    - **Validates: Requirements 5.1**
  
  - [x] 6.3 Implement snapshot serialization and storage
    - Implement SaveSnapshot method with JSON serialization
    - Implement LoadHistory method with JSON deserialization
    - Handle one history file per stream
    - Implement append logic for existing history files
    - _Requirements: 5.2, 5.3, 5.5, 5.6, 6.1, 6.2, 6.5_
  
  - [ ]* 6.4 Write property tests for snapshot serialization and storage
    - **Property 11: Snapshot Serialization Round-Trip**
    - **Property 12: Snapshot Storage Location**
    - **Property 13: One History File Per Stream**
    - **Property 14: Snapshot Append Preserves History**
    - **Validates: Requirements 5.2, 5.3, 5.5, 5.6, 6.3**
  
  - [x] 6.5 Implement snapshot comparison
    - Implement CompareSnapshots method
    - Calculate added, removed, and modified rules
    - _Requirements: 8.2, 8.3, 8.4_
  
  - [ ]* 6.6 Write property test for snapshot diff
    - **Property 17: Snapshot Diff Completeness**
    - **Validates: Requirements 8.2, 8.3, 8.4**
  
  - [x] 6.7 Implement snapshot restore
    - Implement RestoreSnapshot method
    - Apply snapshot rules to stream using P4Service
    - _Requirements: 9.1, 9.2, 9.3_
  
  - [ ]* 6.8 Write property test for snapshot restore
    - **Property 18: Snapshot Restore**
    - **Validates: Requirements 9.1, 9.2**
  
  - [x] 6.9 Implement retention policy
    - Implement ApplyRetentionPolicy method
    - Remove old snapshots based on policy
    - _Requirements: 10.4_

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement MainViewModel
  - [x] 8.1 Create MainViewModel with stream hierarchy properties
    - Implement StreamHierarchy ObservableCollection
    - Implement SelectedStream property with change notification
    - Implement LoadStreamCommand
    - Wire up P4Service for stream loading
    - _Requirements: 1.1, 1.3_
  
  - [x] 8.2 Implement rule display and view mode switching
    - Implement DisplayedRules ObservableCollection
    - Implement CurrentViewMode property
    - Implement RefreshRuleDisplay method
    - Implement ChangeViewMode method
    - Filter rules based on view mode (Local, Inherited, All)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  
  - [ ]* 8.3 Write property test for rule source tracking
    - **Property 5: Rule Source Tracking**
    - **Validates: Requirements 2.5**
  
  - [x] 8.4 Implement rule management commands
    - Implement AddRuleCommand
    - Implement EditRuleCommand
    - Implement DeleteRuleCommand
    - Wire up P4Service for rule operations
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  
  - [x] 8.5 Implement snapshot and settings commands
    - Implement CreateSnapshotCommand
    - Implement OpenHistoryCommand
    - Implement OpenSettingsCommand
    - Wire up SnapshotService and SettingsService
    - _Requirements: 5.1, 10.1_

- [x] 9. Implement HistoryViewModel
  - [x] 9.1 Create HistoryViewModel with snapshot timeline
    - Implement Snapshots ObservableCollection
    - Implement SelectedSnapshot property
    - Implement LoadHistoryCommand
    - Load and display snapshots in chronological order
    - _Requirements: 7.1, 7.2_
  
  - [ ]* 9.2 Write property tests for timeline display
    - **Property 15: Timeline Chronological Ordering**
    - **Property 16: Snapshot Selection Display**
    - **Validates: Requirements 7.1, 7.3**
  
  - [x] 9.3 Implement snapshot comparison in HistoryViewModel
    - Implement ComparisonSnapshot property
    - Implement DiffResults ObservableCollection
    - Implement CompareSnapshotsCommand
    - Wire up SnapshotService.CompareSnapshots
    - _Requirements: 8.1, 8.2, 8.3, 8.4_
  
  - [x] 9.4 Implement snapshot restore in HistoryViewModel
    - Implement RestoreSnapshotCommand
    - Wire up SnapshotService.RestoreSnapshot
    - Handle restore confirmation
    - _Requirements: 9.1, 9.2, 9.4_

- [ ] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [-] 11. Implement WPF Views
  - [x] 11.1 Create MainWindow XAML and code-behind
    - Design layout with TreeView for stream hierarchy
    - Add DataGrid for rule display with three view mode buttons
    - Add toolbar with Add/Edit/Delete/Snapshot buttons
    - Add menu bar with Settings and History options
    - Bind to MainViewModel
    - _Requirements: 1.1, 1.2, 2.1, 12.2, 12.3_
  
  - [x] 11.2 Create RuleDialog for add/edit operations
    - Design dialog with rule type dropdown (ignore/remap)
    - Add path text field with browse button
    - Add remap target field (conditional on rule type)
    - Implement file browser button click handler
    - _Requirements: 3.1, 3.3, 4.1_
  
  - [x] 11.3 Create DepotBrowserDialog
    - Design dialog with TreeView for depot structure
    - Implement navigation through depot folders
    - Implement path selection and OK/Cancel buttons
    - Wire up P4Service for depot browsing
    - _Requirements: 4.2, 4.3, 4.4_
  
  - [ ]* 11.4 Write property test for path selection
    - **Property 9: Path Selection Propagation**
    - **Validates: Requirements 4.3**
  
  - [x] 11.5 Create SettingsDialog
    - Design dialog with fields for P4 connection parameters
    - Add field for history storage depot path
    - Add fields for retention policy (max snapshots, max age)
    - Implement Save/Cancel buttons
    - Wire up SettingsService
    - _Requirements: 10.1, 10.2, 10.3, 10.4_
  
  - [x] 11.6 Create HistoryWindow
    - Design window with timeline ListView for snapshots
    - Add snapshot details panel
    - Add comparison panel with diff display
    - Add Restore button
    - Bind to HistoryViewModel
    - _Requirements: 7.1, 7.2, 7.3, 8.1_

- [x] 12. Implement error handling and logging
  - [x] 12.1 Add error handling to all service methods
    - Handle Perforce connection errors
    - Handle stream operation errors
    - Handle snapshot operation errors
    - Display user-friendly error dialogs
    - _Requirements: Error Handling section_
  
  - [x] 12.2 Implement error logging
    - Create logging service
    - Log all errors with context
    - Store logs in AppData folder
    - _Requirements: Error Handling section_

- [x] 13. Implement progress indicators
  - Add progress dialogs for long-running operations
  - Show progress during stream hierarchy loading
  - Show progress during snapshot operations
  - Show progress during history loading
  - _Requirements: 12.4_

- [x] 14. Final integration and testing
  - [x] 14.1 Wire all components together
    - Ensure MainWindow initializes all services
    - Ensure settings are loaded on startup
    - Ensure P4 connection is established on startup
    - Test all user workflows end-to-end
    - _Requirements: 10.6_
  
  - [x] 14.2 Write integration tests
    - Test complete add rule workflow
    - Test complete snapshot workflow
    - Test complete history and restore workflow
    - _Requirements: All_

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at key milestones
- Property tests validate universal correctness properties using FsCheck
- Unit tests validate specific examples and edge cases
- All Perforce operations use P4API.NET as specified in requirements
