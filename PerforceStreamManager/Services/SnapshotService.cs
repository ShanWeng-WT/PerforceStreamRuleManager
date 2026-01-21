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
                var snapshot = new Snapshot(streamNode.Path, allRules);

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
        /// <param name="historyStoragePath">Base depot path for history storage</param>
        /// <param name="description">Commit description for P4</param>
        /// <exception cref="ArgumentNullException">Thrown when snapshot is null</exception>
        /// <exception cref="Exception">Thrown when save operation fails</exception>
        public void SaveSnapshot(Snapshot snapshot, string historyStoragePath, string description)
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
                string snapshotFilePath = GetSnapshotFilePath(snapshot.StreamPath, historyStoragePath);

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string jsonContent = JsonSerializer.Serialize(snapshot, options);

                // Write to depot - P4 versioning will track history
                _p4Service.WriteDepotFile(snapshotFilePath, jsonContent, description);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"SaveSnapshot({snapshot.StreamPath})");
                throw new Exception($"Failed to save snapshot for stream '{snapshot.StreamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates the depot file path for a stream's snapshot file
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream (e.g., //depot/main)</param>
        /// <param name="historyStoragePath">History storage path - can be a full depot path (//depot/history) 
        /// or a relative path (stream-history) which will be resolved under the stream path</param>
        /// <returns>Full depot path to the snapshot file</returns>
        private string GetSnapshotFilePath(string streamPath, string historyStoragePath)
        {
            // Convert stream path to a safe filename
            // Example: //depot/main/dev -> depot_main_dev.json
            string safeName = streamPath.TrimStart('/').Replace('/', '_');
            string fileName = $"{safeName}.json";

            string basePath;
            
            // Check if historyStoragePath is already a full depot path
            if (historyStoragePath.StartsWith("//"))
            {
                // Already a full depot path, use as-is
                basePath = historyStoragePath.TrimEnd('/');
            }
            else
            {
                // Relative path - resolve it under the stream path itself
                // Example: //OSX/Fish1_1_Patch24 + stream-history -> //OSX/Fish1_1_Patch24/stream-history
                basePath = $"{streamPath.TrimEnd('/')}/{historyStoragePath.TrimStart('/').TrimEnd('/')}";
            }
            
            return $"{basePath}/{fileName}";
        }
    }
}
