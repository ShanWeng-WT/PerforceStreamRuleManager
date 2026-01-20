using PerforceStreamManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Service for managing stream rule snapshots
    /// </summary>
    public class SnapshotService
    {
        private readonly P4Service _p4Service;
        private readonly LoggingService _loggingService;

        public SnapshotService(P4Service p4Service, LoggingService loggingService)
        {
            _p4Service = p4Service ?? throw new ArgumentNullException(nameof(p4Service));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        // Keep default constructor for now but it's deprecated
        public SnapshotService(P4Service p4Service) : this(p4Service, new LoggingService())
        {
        }

        /// <summary>
        /// Creates a snapshot of the current stream rules
        /// </summary>
        /// <param name="streamNode">The stream node to snapshot</param>
        /// <param name="createdBy">User creating the snapshot</param>
        /// <param name="description">Optional description of the snapshot</param>
        /// <returns>A new Snapshot object</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamNode is null</exception>
        public Snapshot CreateSnapshot(StreamNode streamNode, string createdBy, string? description = null)
        {
            if (streamNode == null)
                throw new ArgumentNullException(nameof(streamNode));

            if (string.IsNullOrWhiteSpace(createdBy))
                throw new ArgumentException("Created by cannot be null or empty", nameof(createdBy));

            try
            {
                // Capture all rules (local + inherited)
                var allRules = streamNode.GetAllRules();

                // Create snapshot with captured rules
                var snapshot = new Snapshot(
                    streamNode.Path,
                    createdBy,
                    allRules,
                    description
                );

                return snapshot;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"CreateSnapshot({streamNode.Path})");
                throw;
            }
        }

        /// <summary>
        /// Saves a snapshot to the depot history file
        /// </summary>
        /// <param name="snapshot">The snapshot to save</param>
        /// <param name="historyStoragePath">Base depot path for history storage</param>
        /// <exception cref="ArgumentNullException">Thrown when snapshot is null</exception>
        /// <exception cref="Exception">Thrown when save operation fails</exception>
        public void SaveSnapshot(Snapshot snapshot, string historyStoragePath)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrWhiteSpace(historyStoragePath))
                throw new ArgumentException("History storage path cannot be null or empty", nameof(historyStoragePath));

            if (string.IsNullOrWhiteSpace(snapshot.StreamPath))
                throw new ArgumentException("Snapshot stream path cannot be null or empty");

            try
            {
                _loggingService.LogInfo($"Saving snapshot for stream: {snapshot.StreamPath}");

                // Generate history file path for this stream
                string historyFilePath = GetHistoryFilePath(snapshot.StreamPath, historyStoragePath);

                // Load existing history or create new
                var historyFile = LoadHistoryFile(historyFilePath);

                // Add the new snapshot to the history
                historyFile.Snapshots.Add(snapshot);

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string jsonContent = JsonSerializer.Serialize(historyFile, options);

                // Write to depot
                string description = $"[{snapshot.CreatedBy}] Update Stream Rule: {snapshot.StreamPath}";
                _p4Service.WriteDepotFile(historyFilePath, jsonContent, description);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"SaveSnapshot({snapshot.StreamPath})");
                throw new Exception($"Failed to save snapshot for stream '{snapshot.StreamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads snapshot history for a stream
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream</param>
        /// <param name="historyStoragePath">Base depot path for history storage</param>
        /// <returns>List of snapshots in chronological order</returns>
        /// <exception cref="Exception">Thrown when load operation fails</exception>
        public List<Snapshot> LoadHistory(string streamPath, string historyStoragePath)
        {
            if (string.IsNullOrWhiteSpace(streamPath))
                throw new ArgumentException("Stream path cannot be null or empty", nameof(streamPath));

            if (string.IsNullOrWhiteSpace(historyStoragePath))
                throw new ArgumentException("History storage path cannot be null or empty", nameof(historyStoragePath));

            try
            {
                _loggingService.LogInfo($"Loading history for stream: {streamPath}");
                string historyFilePath = GetHistoryFilePath(streamPath, historyStoragePath);
                var historyFile = LoadHistoryFile(historyFilePath);
                
                // Return snapshots sorted by timestamp
                return historyFile.Snapshots.OrderBy(s => s.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"LoadHistory({streamPath})");
                throw new Exception($"Failed to load history for stream '{streamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates the depot file path for a stream's history file
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream</param>
        /// <param name="historyStoragePath">Base depot path for history storage</param>
        /// <returns>Full depot path to the history file</returns>
        private string GetHistoryFilePath(string streamPath, string historyStoragePath)
        {
            // Convert stream path to a safe filename
            // Example: //depot/main/dev -> depot_main_dev.json
            string safeName = streamPath.TrimStart('/').Replace('/', '_');
            string fileName = $"{safeName}.json";

            // Ensure history storage path ends with /
            string basePath = historyStoragePath.TrimEnd('/');
            
            return $"{basePath}/{fileName}";
        }

        /// <summary>
        /// Loads a history file from the depot, or creates a new one if it doesn't exist
        /// </summary>
        /// <param name="historyFilePath">Full depot path to the history file</param>
        /// <returns>HistoryFile object</returns>
        private HistoryFile LoadHistoryFile(string historyFilePath)
        {
            try
            {
                // Try to read existing file
                string jsonContent = _p4Service.ReadDepotFile(historyFilePath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var historyFile = JsonSerializer.Deserialize<HistoryFile>(jsonContent, options);
                
                if (historyFile == null)
                {
                    throw new Exception("Failed to deserialize history file");
                }

                return historyFile;
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("File not found"))
                {
                    _loggingService.LogInfo($"Creating new history file for: {historyFilePath} (Reason: {ex.Message})");
                }
                
                // File doesn't exist or is corrupted, create new
                return new HistoryFile
                {
                    StreamPath = ExtractStreamPathFromFileName(historyFilePath),
                    Snapshots = new List<Snapshot>()
                };
            }
        }

        /// <summary>
        /// Extracts the stream path from a history file name
        /// </summary>
        /// <param name="historyFilePath">Full depot path to the history file</param>
        /// <returns>Stream path</returns>
        private string ExtractStreamPathFromFileName(string historyFilePath)
        {
            // Extract filename from path
            string fileName = historyFilePath.Split('/').Last();
            
            // Remove .json extension
            string nameWithoutExt = fileName.Replace(".json", "");
            
            // Convert back to stream path format
            // depot_main_dev -> //depot/main/dev
            return "//" + nameWithoutExt.Replace('_', '/');
        }

        /// <summary>
        /// Compares two snapshots and calculates the differences
        /// </summary>
        /// <param name="snapshot1">The first (older) snapshot</param>
        /// <param name="snapshot2">The second (newer) snapshot</param>
        /// <returns>SnapshotDiff object containing added, removed, and modified rules</returns>
        /// <exception cref="ArgumentNullException">Thrown when either snapshot is null</exception>
        public SnapshotDiff CompareSnapshots(Snapshot snapshot1, Snapshot snapshot2)
        {
            if (snapshot1 == null)
                throw new ArgumentNullException(nameof(snapshot1));

            if (snapshot2 == null)
                throw new ArgumentNullException(nameof(snapshot2));

            try 
            {
                var diff = new SnapshotDiff();

                // Create dictionaries for efficient lookup
                // Key: combination of Type and Path (unique identifier for a rule)
                var rules1Dict = snapshot1.Rules.ToDictionary(r => GetRuleKey(r), r => r);
                var rules2Dict = snapshot2.Rules.ToDictionary(r => GetRuleKey(r), r => r);

                // Find added rules (in snapshot2 but not in snapshot1)
                foreach (var rule2 in snapshot2.Rules)
                {
                    string key = GetRuleKey(rule2);
                    if (!rules1Dict.ContainsKey(key))
                    {
                        diff.AddedRules.Add(rule2);
                    }
                }

                // Find removed rules (in snapshot1 but not in snapshot2)
                foreach (var rule1 in snapshot1.Rules)
                {
                    string key = GetRuleKey(rule1);
                    if (!rules2Dict.ContainsKey(key))
                    {
                        diff.RemovedRules.Add(rule1);
                    }
                }

                // Find modified rules (same Type and Path, but different RemapTarget or SourceStream)
                foreach (var rule1 in snapshot1.Rules)
                {
                    string key = GetRuleKey(rule1);
                    if (rules2Dict.TryGetValue(key, out var rule2))
                    {
                        // Check if the rule has been modified
                        if (!AreRulesEqual(rule1, rule2))
                        {
                            diff.ModifiedRules.Add(new RuleChange(rule1, rule2));
                        }
                    }
                }

                return diff;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "CompareSnapshots");
                throw;
            }
        }

        /// <summary>
        /// Restores a stream to a previous snapshot state
        /// </summary>
        /// <param name="snapshot">The snapshot to restore</param>
        /// <exception cref="ArgumentNullException">Thrown when snapshot is null</exception>
        /// <exception cref="Exception">Thrown when restore operation fails</exception>
        public void RestoreSnapshot(Snapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrWhiteSpace(snapshot.StreamPath))
                throw new ArgumentException("Snapshot stream path cannot be null or empty");

            try
            {
                _loggingService.LogInfo($"Restoring snapshot for stream: {snapshot.StreamPath}, Timestamp: {snapshot.Timestamp}");

                // Get only the local rules from the snapshot
                // (filter out inherited rules since we only want to restore local rules)
                var localRules = snapshot.Rules
                    .Where(r => r.SourceStream == snapshot.StreamPath)
                    .ToList();

                // Update the stream with the snapshot's local rules
                _p4Service.UpdateStreamRules(snapshot.StreamPath, localRules);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"RestoreSnapshot({snapshot.StreamPath})");
                throw new Exception($"Failed to restore snapshot for stream '{snapshot.StreamPath}': {ex.Message}", ex);
            }
        }



        /// <summary>
        /// Generates a unique key for a rule based on Type and Path
        /// </summary>
        /// <param name="rule">The rule to generate a key for</param>
        /// <returns>Unique key string</returns>
        private string GetRuleKey(StreamRule rule)
        {
            return $"{rule.Type}|{rule.Path}";
        }

        /// <summary>
        /// Checks if two rules are equal (all properties match)
        /// </summary>
        /// <param name="rule1">First rule</param>
        /// <param name="rule2">Second rule</param>
        /// <returns>True if rules are equal, false otherwise</returns>
        private bool AreRulesEqual(StreamRule rule1, StreamRule rule2)
        {
            return rule1.Type == rule2.Type &&
                   rule1.Path == rule2.Path &&
                   rule1.RemapTarget == rule2.RemapTarget &&
                   rule1.SourceStream == rule2.SourceStream;
        }

        /// <summary>
        /// Internal class for JSON serialization of history files
        /// </summary>
        private class HistoryFile
        {
            public string StreamPath { get; set; } = "";
            public List<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
        }
    }
}