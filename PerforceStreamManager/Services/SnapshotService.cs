using PerforceStreamManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Service for managing stream rule snapshots.
    /// Snapshots are stored in depot files; P4's versioning provides history.
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
        /// <returns>A new Snapshot object</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamNode is null</exception>
        public Snapshot CreateSnapshot(StreamNode streamNode)
        {
            if (streamNode == null)
                throw new ArgumentNullException(nameof(streamNode));

            try
            {
                // Capture all rules (local + inherited)
                var allRules = streamNode.GetAllRules();

                // Create snapshot with captured rules
                var snapshot = new Snapshot(allRules);

                return snapshot;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"CreateSnapshot({streamNode.Path})");
                throw;
            }
        }

        /// <summary>
        /// Saves a snapshot to the depot file.
        /// P4's versioning will track the history automatically.
        /// </summary>
        /// <param name="snapshot">The snapshot to save</param>
        /// <param name="streamPath">The stream path used to determine the snapshot file location</param>
        /// <param name="historyStoragePath">Base depot path for history storage</param>
        /// <param name="description">Commit description for P4</param>
        /// <param name="submitImmediately">If true, submit the file immediately; otherwise leave it pending</param>
        /// <exception cref="ArgumentNullException">Thrown when snapshot is null</exception>
        /// <exception cref="Exception">Thrown when save operation fails</exception>
        public void SaveSnapshot(Snapshot snapshot, string streamPath, string historyStoragePath, string description, bool submitImmediately = true)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrWhiteSpace(streamPath))
                throw new ArgumentException("Stream path cannot be null or empty", nameof(streamPath));

            if (string.IsNullOrWhiteSpace(historyStoragePath))
                throw new ArgumentException("History storage path cannot be null or empty", nameof(historyStoragePath));

            try
            {
                _loggingService.LogInfo($"Saving snapshot for stream: {streamPath}");

                // Generate history file path for this stream
                string snapshotFilePath = GetSnapshotFilePath(streamPath, historyStoragePath);

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string jsonContent = JsonSerializer.Serialize(snapshot, options);

                // Write to depot - P4 versioning will track history
                _p4Service.WriteDepotFile(snapshotFilePath, jsonContent, description, submitImmediately);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"SaveSnapshot({streamPath})");
                throw new Exception($"Failed to save snapshot for stream '{streamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a snapshot from JSON content
        /// </summary>
        /// <param name="jsonContent">JSON string containing the snapshot</param>
        /// <returns>Deserialized Snapshot object</returns>
        /// <exception cref="ArgumentException">Thrown when jsonContent is null or empty</exception>
        /// <exception cref="Exception">Thrown when deserialization fails</exception>
        public Snapshot LoadSnapshot(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
                throw new ArgumentException("JSON content cannot be null or empty", nameof(jsonContent));

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var snapshot = JsonSerializer.Deserialize<Snapshot>(jsonContent, options);
                
                if (snapshot == null)
                    throw new Exception("Deserialization returned null");

                _loggingService.LogInfo($"Loaded snapshot with {snapshot.Rules.Count} rules");
                return snapshot;
            }
            catch (JsonException ex)
            {
                _loggingService.LogError(ex, "LoadSnapshot - JSON parsing error");
                throw new Exception($"Failed to parse snapshot JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "LoadSnapshot");
                throw;
            }
        }

        /// <summary>
        /// Generates the depot file path for a stream's snapshot file.
        /// The filename is derived from the historyStoragePath setting.
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream (e.g., //depot/main)</param>
        /// <param name="historyStoragePath">History storage path - can be a full depot path (//depot/history/snapshots) 
        /// or a relative path (stream-history). The last segment becomes the filename.
        /// Examples:
        ///   - "stream-history" -> {streamPath}/stream-history.json
        ///   - "stream-history/history" -> {streamPath}/stream-history/history.json
        ///   - "//depot/history" -> //depot/history.json
        ///   - "//depot/history/snapshots" -> //depot/history/snapshots.json
        /// </param>
        /// <returns>Full depot path to the snapshot file</returns>
        public string GetSnapshotFilePath(string streamPath, string historyStoragePath)
        {
            // Normalize the path
            string normalizedPath = historyStoragePath.Trim().TrimEnd('/');
            
            // Split path into directory and filename
            int lastSlashIndex = normalizedPath.LastIndexOf('/');
            string directory;
            string fileName;
            
            if (lastSlashIndex >= 0)
            {
                directory = normalizedPath.Substring(0, lastSlashIndex);
                fileName = normalizedPath.Substring(lastSlashIndex + 1) + ".json";
            }
            else
            {
                // No slash - just a filename (e.g., "stream-history")
                directory = "";
                fileName = normalizedPath + ".json";
            }
            
            // Check if historyStoragePath is already a full depot path
            if (normalizedPath.StartsWith("//"))
            {
                // Full depot path - use the directory portion as-is
                if (string.IsNullOrEmpty(directory) || directory == "/")
                {
                    // Edge case: just "//depot" - use as directory
                    return $"{normalizedPath}/{fileName}";
                }
                return $"{directory}/{fileName}";
            }
            else
            {
                // Relative path - resolve it under the stream path
                string basePath = streamPath.TrimEnd('/');
                if (string.IsNullOrEmpty(directory))
                {
                    // Just a filename, place at stream root
                    return $"{basePath}/{fileName}";
                }
                return $"{basePath}/{directory}/{fileName}";
            }
        }
    }
}
