using Perforce.P4;
using PerforceStreamManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Timers;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Service for managing Perforce operations using P4API.NET
    /// </summary>
    public class P4Service : IDisposable
    {
        private readonly LoggingService _loggingService;
        public LoggingService Logger => _loggingService;
        private readonly ConnectionRateLimiter _rateLimiter;
        private bool _rateLimitingEnabled = true;
        private Server? _server;
        private Repository? _repository;
        private Connection? _connection;
        private P4ConnectionSettings? _settings;
        private DateTime? _serverVersionTime; // Cached server version (e.g., 2019, 2020, 2021)

        // Session timeout fields
        private DateTime _lastActivityTime;
        private System.Timers.Timer? _sessionTimer;
        private TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);
        private bool _sessionTimeoutEnabled = true;

        /// <summary>
        /// Event raised when the session expires due to inactivity.
        /// Handlers should perform cleanup and notify the user.
        /// </summary>
        public event EventHandler? OnSessionExpired;

        public P4Service(LoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _rateLimiter = new ConnectionRateLimiter();
        }

        /// <summary>
        /// Gets whether the service is currently connected to Perforce
        /// </summary>
        public bool IsConnected => _connection != null && _connection.Status == ConnectionStatus.Connected;

        /// <summary>
        /// Configures the session timeout. Call this before Connect() to set custom timeout.
        /// </summary>
        /// <param name="timeoutMinutes">Timeout in minutes. Set to 0 to disable timeout.</param>
        public void SetSessionTimeout(int timeoutMinutes)
        {
            if (timeoutMinutes <= 0)
            {
                _sessionTimeoutEnabled = false;
                _sessionTimeout = TimeSpan.Zero;
                _loggingService.LogInfo("Session timeout disabled");
            }
            else
            {
                _sessionTimeoutEnabled = true;
                _sessionTimeout = TimeSpan.FromMinutes(timeoutMinutes);
                _loggingService.LogInfo($"Session timeout set to {timeoutMinutes} minutes");
            }
        }

        /// <summary>
        /// Configures connection rate limiting. Call this before Connect() to enable/disable.
        /// </summary>
        /// <param name="enabled">Whether to enable rate limiting</param>
        public void SetRateLimiting(bool enabled)
        {
            _rateLimitingEnabled = enabled;
            _loggingService.LogInfo($"Connection rate limiting {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Gets the current rate limiter instance (for UI feedback).
        /// </summary>
        public ConnectionRateLimiter RateLimiter => _rateLimiter;

        /// <summary>
        /// Initializes the session inactivity timer.
        /// </summary>
        private void InitializeSessionTimer()
        {
            if (!_sessionTimeoutEnabled)
                return;

            StopSessionTimer();

            _lastActivityTime = DateTime.Now;
            _sessionTimer = new System.Timers.Timer(60000); // Check every minute
            _sessionTimer.Elapsed += OnSessionTimerElapsed;
            _sessionTimer.AutoReset = true;
            _sessionTimer.Start();

            _loggingService.LogInfo($"Session timer started with {_sessionTimeout.TotalMinutes} minute timeout");
        }

        /// <summary>
        /// Stops and disposes the session timer.
        /// </summary>
        private void StopSessionTimer()
        {
            if (_sessionTimer != null)
            {
                _sessionTimer.Stop();
                _sessionTimer.Elapsed -= OnSessionTimerElapsed;
                _sessionTimer.Dispose();
                _sessionTimer = null;
            }
        }

        /// <summary>
        /// Timer elapsed handler - checks if session has timed out due to inactivity.
        /// </summary>
        private void OnSessionTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsConnected || !_sessionTimeoutEnabled)
                return;

            var idleTime = DateTime.Now - _lastActivityTime;
            if (idleTime > _sessionTimeout)
            {
                _loggingService.LogInfo($"Session expired due to inactivity ({idleTime.TotalMinutes:F1} minutes idle)");

                // Stop the timer to prevent multiple events
                StopSessionTimer();

                // Disconnect
                Disconnect();

                // Raise the event
                OnSessionExpired?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Updates the last activity time to prevent session timeout.
        /// Call this from methods that perform P4 operations.
        /// </summary>
        private void UpdateActivityTime()
        {
            _lastActivityTime = DateTime.Now;
        }

        /// <summary>
        /// Connects to Perforce server with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings including server, port, user, and workspace</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null</exception>
        /// <exception cref="Exception">Thrown when connection fails</exception>
        public void Connect(P4ConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            ConnectInternal(settings, () =>
            {
                if (!string.IsNullOrEmpty(settings.Password))
                    return _connection!.Login(settings.Password);
                return _connection!.Login(null);
            });
        }

        /// <summary>
        /// Connects to Perforce server with the specified settings and SecureString password.
        /// This is the preferred method for connecting as it keeps the password more secure.
        /// </summary>
        /// <param name="settings">Connection settings (password field is ignored)</param>
        /// <param name="securePassword">SecureString containing the password</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null</exception>
        /// <exception cref="Exception">Thrown when connection fails</exception>
        public void Connect(P4ConnectionSettings settings, SecureString securePassword)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            ConnectInternal(settings, () =>
            {
                if (securePassword != null && securePassword.Length > 0)
                {
                    string? password = SecureCredentialManager.ToPlainString(securePassword);
                    try
                    {
                        return _connection!.Login(password);
                    }
                    finally
                    {
                        password = null;
                    }
                }
                return _connection!.Login(null);
            });
        }

        /// <summary>
        /// Internal connection logic shared by both Connect overloads
        /// </summary>
        private void ConnectInternal(P4ConnectionSettings settings, Func<Credential?> loginAction)
        {
            // Check rate limiting
            if (_rateLimitingEnabled && !_rateLimiter.CanAttemptConnection(out TimeSpan waitTime))
            {
                int waitSeconds = (int)Math.Ceiling(waitTime.TotalSeconds);
                string message = $"Too many failed connection attempts. Please wait {waitSeconds} seconds before trying again.";
                _loggingService.LogWarning($"Connection rate limited. Wait time: {waitSeconds}s");
                throw new Exception(message);
            }

            try
            {
                _loggingService.LogInfo($"Connecting to Perforce server: {settings.Server}:{settings.Port}, User: {settings.User}");

                if (IsConnected)
                {
                    Disconnect();
                }

                _settings = settings;
                string serverUri = $"{settings.Server}:{settings.Port}";
                _server = new Server(new ServerAddress(serverUri));

                if (serverUri.StartsWith("ssl:", StringComparison.OrdinalIgnoreCase))
                {
                    _loggingService.LogInfo("SSL connection detected. Using system certificate store for validation.");
                }

                _repository = new Repository(_server);
                _connection = _repository.Connection;
                _connection.UserName = settings.User;
                _connection.Client = new Client();
                _connection.Connect(null);

                Credential? credential = loginAction();
                if (credential == null)
                {
                    throw new Exception("Authentication failed. Please check your credentials.");
                }

                _loggingService.LogInfo("Successfully connected to Perforce.");

                if (_rateLimitingEnabled)
                {
                    _rateLimiter.RecordSuccessfulConnection();
                }

                InitializeSessionTimer();
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, "Connect");
                if (_rateLimitingEnabled)
                {
                    _rateLimiter.RecordFailedAttempt();
                }
                Disconnect();
                throw new Exception($"Failed to connect to Perforce server: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Connect");
                if (_rateLimitingEnabled && !ex.Message.Contains("Too many failed connection attempts"))
                {
                    _rateLimiter.RecordFailedAttempt();
                }
                Disconnect();
                throw new Exception($"Unexpected error connecting to Perforce: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disconnects from the Perforce server
        /// </summary>
        public void Disconnect()
        {
            try
            {
                // Stop session timer first
                StopSessionTimer();

                if (_connection != null)
                {
                    if (_connection.Status == ConnectionStatus.Connected)
                    {
                        _connection.Disconnect();
                    }
                    _connection = null;
                }

                _repository = null;
                _server = null;
                _settings = null;
                _serverVersionTime = null; // Clear cached server version
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Disconnect");
                // Suppress exceptions during disconnect
                StopSessionTimer();
                _connection = null;
                _repository = null;
                _server = null;
                _settings = null;
                _serverVersionTime = null;
            }
        }

        /// <summary>
        /// Ensures the service is connected, throwing an exception if not.
        /// Also resets the activity timer to prevent session timeout.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                var ex = new InvalidOperationException("Not connected to Perforce server. Call Connect() first.");
                _loggingService.LogError(ex, "EnsureConnected");
                throw ex;
            }

            // Reset activity timer on successful operation
            UpdateActivityTime();
        }

        /// <summary>
        /// Gets the Perforce server version year (e.g., 2019, 2020, 2021)
        /// </summary>
        /// <returns>Server version year, or null if unable to determine</returns>
        private DateTime? GetServerVersionTime()
        {
            // Return cached version if available
            if (_serverVersionTime.HasValue)
                return _serverVersionTime;

            if (!IsConnected)
                return null;

            try
            {
                return _repository!.Server?.Metadata?.Version?.Date;
            }
            catch (Exception ex)
            {
                _loggingService.LogInfo($"Failed to detect server version: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets stream information from Perforce safely, returning null if not found or on error
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream</param>
        /// <returns>Stream object or null</returns>
        public Stream? GetStreamSafe(string streamPath)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(streamPath))
                return null;

            UpdateActivityTime();

            try
            {
                return _repository!.GetStream(streamPath);
            }
            catch
            {
                // Ignore errors (e.g. not a stream)
                return null;
            }
        }

        /// <summary>
        /// Gets stream information from Perforce
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream (e.g., //depot/main)</param>
        /// <returns>Stream object from P4API.NET</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when stream path is invalid</exception>
        /// <exception cref="Exception">Thrown when stream retrieval fails</exception>
        public Stream GetStream(string streamPath)
        {
            EnsureConnected();

            // Validate stream path for security
            P4InputValidator.ValidateStreamPathOrThrow(streamPath, nameof(streamPath));

            try
            {
                var stream = _repository!.GetStream(streamPath);
                if (stream == null)
                {
                    throw new Exception($"Stream not found: {streamPath}");
                }
                return stream;
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetStream({streamPath})");
                throw new Exception($"Failed to get stream '{streamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the complete stream hierarchy starting from a root stream
        /// </summary>
        /// <param name="rootStreamPath">Full depot path of the root stream</param>
        /// <returns>List of StreamNode objects representing the hierarchy</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when stream path is invalid</exception>
        /// <exception cref="Exception">Thrown when hierarchy retrieval fails</exception>
        public List<StreamNode> GetStreamHierarchy(string rootStreamPath)
        {
            EnsureConnected();

            // Validate stream path for security
            P4InputValidator.ValidateStreamPathOrThrow(rootStreamPath, nameof(rootStreamPath));

            try
            {
                _loggingService.LogInfo($"Loading stream hierarchy from root: {rootStreamPath}");
                var rootStream = GetStream(rootStreamPath);
                var rootNode = BuildStreamNode(rootStream, null);

                // Build hierarchy recursively
                BuildChildHierarchy(rootNode);

                return new List<StreamNode> { rootNode };
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetStreamHierarchy({rootStreamPath})");
                throw new Exception($"Failed to get stream hierarchy for '{rootStreamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Builds a StreamNode from a P4 Stream object
        /// </summary>
        private StreamNode BuildStreamNode(Stream stream, StreamNode? parent)
        {
            var node = new StreamNode
            {
                Name = stream.Name ?? stream.Id,
                Path = stream.Id,
                Parent = parent!,
                Children = new List<StreamNode>(),
                LocalRules = GetStreamRules(stream.Id)
            };

            return node;
        }

        /// <summary>
        /// Recursively builds child hierarchy for a stream node
        /// </summary>
        private void BuildChildHierarchy(StreamNode node)
        {
            try
            {
                // Get all streams and find children of this node
                var allStreams = GetAllStreams();
                
                foreach (var stream in allStreams)
                {
                    // Check if this stream's parent matches our node
                    string? parentPath = stream.Parent?.ToString();
                    if (parentPath != null && string.Equals(parentPath, node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        var childNode = BuildStreamNode(stream, node);
                        node.Children.Add(childNode);
                        
                        // Recursively build children
                        BuildChildHierarchy(childNode);
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"BuildChildHierarchy({node.Path})");
                // If we can't get children, just leave the children list empty
                // This allows partial hierarchy display even if some streams are inaccessible
            }
        }

        /// <summary>
        /// Gets all streams from the Perforce server
        /// </summary>
        private List<Stream> GetAllStreams()
        {
            EnsureConnected();

            try
            {
                var streams = _repository!.GetStreams(new StreamsCmdOptions(StreamsCmdFlags.None, null, null, null, -1));
                return streams?.ToList() ?? new List<Stream>();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "GetAllStreams");
                return new List<Stream>();
            }
        }

        /// <summary>
        /// Gets the rules (ignore/remap paths) for a specific stream
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream</param>
        /// <returns>List of StreamRule objects</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when stream path is invalid</exception>
        /// <exception cref="Exception">Thrown when rule retrieval fails</exception>
        public List<StreamRule> GetStreamRules(string streamPath)
        {
            EnsureConnected();

            // Validate stream path for security
            P4InputValidator.ValidateStreamPathOrThrow(streamPath, nameof(streamPath));

            try
            {
                var stream = GetStream(streamPath);
                var rules = new List<StreamRule>();

                // Parse Paths (which might contain exclude rules)
                if (stream.Paths != null)
                {
                    foreach (var pathEntry in stream.Paths)
                    {
                        // We only care about explicit excludes in Paths if they exist
                        if (pathEntry.Type == MapType.Exclude)
                        {
                            var rule = ParsePathEntry(pathEntry, streamPath);
                            if (rule != null)
                            {
                                rules.Add(rule);
                            }
                        }
                    }
                }

                // Parse Remapped
                if (stream.Remapped != null)
                {
                    foreach (var entry in stream.Remapped)
                    {
                        if (entry.Left != null)
                        {
                            // Remapped entries are typically "source target"
                            rules.Add(new StreamRule(
                                "remap", 
                                entry.Left.Path, 
                                entry.Right?.Path ?? "", 
                                streamPath));
                        }
                    }
                }

                // Parse Ignored
                if (stream.Ignored != null)
                {
                    foreach (var entry in stream.Ignored)
                    {
                        if (entry.Left != null)
                        {
                            // Ignored entries are just paths
                            rules.Add(new StreamRule(
                                "ignore",
                                entry.Left.Path,
                                remapTarget: null,
                                sourceStream: streamPath));
                        }
                    }
                }

                return rules;
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetStreamRules({streamPath})");
                throw new Exception($"Failed to get rules for stream '{streamPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a MapEntry into a StreamRule
        /// </summary>
        private StreamRule? ParsePathEntry(MapEntry pathEntry, string sourceStream)
        {
            if (pathEntry == null)
                return null;

            string ruleType;
            string path;
            string? remapTarget = null;

            // MapEntry.Type can be: Include, Exclude, Overlay
            switch (pathEntry.Type)
            {
                case MapType.Exclude:
                    ruleType = "ignore";
                    path = pathEntry.Left?.ToString() ?? "";
                    break;

                case MapType.Include:
                case MapType.Overlay:
                    ruleType = "remap";
                    path = pathEntry.Left?.ToString() ?? "";
                    remapTarget = pathEntry.Right?.ToString();
                    break;

                default:
                    // Unknown type, skip
                    return null;
            }

            if (string.IsNullOrWhiteSpace(path))
                return null;

            return new StreamRule(ruleType, path, remapTarget ?? "", sourceStream);
        }

        /// <summary>
        /// Updates the rules for a specific stream
        /// </summary>
        /// <param name="streamPath">Full depot path of the stream</param>
        /// <param name="rules">List of rules to set for the stream</param>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when stream path is invalid</exception>
        /// <exception cref="Exception">Thrown when update fails</exception>
        public void UpdateStreamRules(string streamPath, List<StreamRule> rules)
        {
            EnsureConnected();

            // Validate stream path for security
            P4InputValidator.ValidateStreamPathOrThrow(streamPath, nameof(streamPath));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            try
            {
                _loggingService.LogInfo($"Updating rules for stream: {streamPath}");
                var stream = GetStream(streamPath);

                // Prepare collections
                var remapped = new ViewMap();
                var ignored = new ViewMap();

                // Separate rules into categories
                foreach (var rule in rules)
                {
                    if (string.Equals(rule.Type, "remap", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add to Remapped
                        if (!string.IsNullOrWhiteSpace(rule.Path) && !string.IsNullOrWhiteSpace(rule.RemapTarget))
                        {
                            remapped.Add(new MapEntry(MapType.Include, new DepotPath(rule.Path),
                                new DepotPath(rule.RemapTarget)));
                        }
                    }
                    else if (string.Equals(rule.Type, "ignore", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add to Ignored
                        if (!string.IsNullOrWhiteSpace(rule.Path))
                        {
                            // Ignored list usually uses just the left side
                            ignored.Add(new MapEntry(MapType.Include, new DepotPath(rule.Path), null));
                        }
                    }
                }

                // Update stream properties
                stream.Remapped = remapped;
                stream.Ignored = ignored;
                
                // Also clean up Paths: remove any explicit excludes since we are moving them to Ignored
                // This prevents duplication if we read from Paths but save to Ignored
                if (stream.Paths != null)
                {
                    var cleanPaths = new ViewMap();
                    foreach (var path in stream.Paths)
                    {
                        if (path.Type != MapType.Exclude)
                        {
                            cleanPaths.Add(path);
                        }
                    }
                    stream.Paths = cleanPaths;
                }

                // Get server version to determine if reflection workaround is needed
                var serverVersionTime = GetServerVersionTime();
                
                // Only apply reflection workaround for servers older than 2020
                // This was needed for older P4API.NET versions to properly format Remapped/Ignored sections
                if (serverVersionTime.HasValue && serverVersionTime.Value.Year < 2020)
                {
                    _loggingService.LogInfo($"Applying reflection workaround for server version {serverVersionTime.Value}");
                    
                    Type streamType = typeof(Stream);
                    
                    System.Reflection.FieldInfo? field = streamType.GetField("StreamSpecFormatPre202", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (field != null)
                    {
                        var original = field.GetValue(stream) as string;
                        if (!string.IsNullOrEmpty(original))
                        {
                            var newFormat = "Stream:\t{0}\n\nUpdate:\t{1}\n\nAccess:\t{2}\n\nOwner:\t{3}\n\nName:\t{4}\n\nParent:\t{5}\n\nType:\t{6}\n\nDescription:\n\t{7}\n\nOptions:\t{8}\n\n\nPaths:\n\t{9}\n\nRemapped:\n\t{10}\n\nIgnored:\n\t{11}\n\n{12}";
                            field.SetValue(stream, newFormat); 
                        }
                    }
                }
                else
                {
                    _loggingService.LogInfo($"Server version {serverVersionTime?.ToString() ?? "unknown"} - skipping reflection workaround");
                }
                
                // Save the stream
                _repository!.UpdateStream(stream);
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"UpdateStreamRules({streamPath})");
                throw new Exception($"Failed to update rules for stream '{streamPath}': {ex.Message}", ex);
            }
            catch (Exception e)
            {
                _loggingService.LogError(e, $"UpdateStreamRules({streamPath}) exception:{e.Message}  | {e.InnerException?.Message}");
                throw;
            }
        }



        /// <summary>
        /// Gets files in a depot directory
        /// </summary>
        /// <param name="depotPath">Depot path to query (e.g., //depot/main/...)</param>
        /// <returns>List of depot file paths</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when file retrieval fails</exception>
        public List<string> GetDepotFiles(string depotPath)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            try
            {
                _loggingService.LogInfo($"GetDepotFiles: Querying files for {depotPath} using GetFileMetaData");
                
                var fileSpecs = new List<FileSpec> { new FileSpec(new DepotPath(depotPath)) };
                
                // maxResults: -1 for unlimited. Original code used 0 which might have caused empty results.
                var options = new GetFileMetaDataCmdOptions(
                    GetFileMetadataCmdFlags.None,
                    null, null, -1, null, null, null
                );

                var files = _repository!.GetFileMetaData(fileSpecs, options);
                var filePaths = new List<string>();

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file != null && file.DepotPath != null)
                        {
                            filePaths.Add(file.DepotPath.Path);
                        }
                    }
                }
                
                _loggingService.LogInfo($"GetDepotFiles: Found {filePaths.Count} files");
                return filePaths;
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetDepotFiles({depotPath})");
                throw new Exception($"Failed to get depot files from '{depotPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets directories in a depot path
        /// </summary>
        /// <param name="depotPath">Depot path to query (e.g., //depot/main/*)</param>
        /// <returns>List of depot directory paths</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when directory retrieval fails</exception>
        public List<string> GetDepotDirectories(string depotPath)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            try
            {
                // Use 'p4 dirs' command to get directories
                // Ensure path ends with /* for directory listing
                if (!depotPath.EndsWith("/*"))
                {
                    // Special handling for root path to avoid trimming "//" to empty string
                    if (depotPath == "//" || depotPath == "/")
                    {
                        depotPath = "//*";
                    }
                    else
                    {
                        depotPath = depotPath.TrimEnd('/') + "/*";
                    }
                }
                
                _loggingService.LogInfo($"GetDepotDirectories: Running 'p4 dirs {depotPath}'");

                // Use P4Command to run dirs command
                var cmd = new P4Command(_repository!, "dirs", true, depotPath);
                var results = cmd.Run();
                var dirPaths = new List<string>();

                if (results != null)
                {
                    if (results.TaggedOutput != null)
                    {
                        foreach (var taggedObject in results.TaggedOutput)
                        {
                            if (taggedObject.ContainsKey("dir"))
                            {
                                var dir = taggedObject["dir"];
                                if (!string.IsNullOrWhiteSpace(dir))
                                {
                                    dirPaths.Add(dir);
                                }
                            }
                        }
                    }
                    
                    // Log details about the result for debugging
                    int taggedCount = results.TaggedOutput?.Count ?? 0;
                    int infoCount = results.InfoOutput?.Count ?? 0;
                    int errorCount = results.ErrorList?.Count ?? 0;
                    
                    _loggingService.LogInfo($"GetDepotDirectories: Result counts - Tagged: {taggedCount}, Info: {infoCount}, Error: {errorCount}");
                    
                    if (errorCount > 0 && results.ErrorList != null)
                    {
                        foreach (var err in results.ErrorList)
                        {
                            _loggingService.LogInfo($"GetDepotDirectories Error: {err.ErrorMessage}");
                        }
                    }
                }
                else
                {
                    _loggingService.LogInfo("GetDepotDirectories: Results object was null");
                }

                _loggingService.LogInfo($"GetDepotDirectories: Returned {dirPaths.Count} directories");
                return dirPaths;
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetDepotDirectories({depotPath})");
                throw new Exception($"Failed to get depot directories from '{depotPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads the content of a depot file
        /// </summary>
        /// <param name="depotPath">Full depot path to the file</param>
        /// <returns>File content as string</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when file read fails</exception>
        public string ReadDepotFile(string depotPath)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            try
            {
                // Use 'print -o' to output file content to a secure temp file
                // This is the most reliable method with P4API.NET as the API doesn't
                // populate TextOutput/BinaryOutput for the print command
                using (var tempFileManager = new SecureTempFileManager())
                {
                    var cmd = new P4Command(_repository!, "print", false, "-q", "-o", tempFileManager.FilePath, depotPath);
                    var result = cmd.Run();

                    if (result.ErrorList != null && result.ErrorList.Count > 0)
                    {
                        throw new P4Exception(result.ErrorList[0]);
                    }

                    if (System.IO.File.Exists(tempFileManager.FilePath))
                    {
                        return System.IO.File.ReadAllText(tempFileManager.FilePath);
                    }

                    return string.Empty;
                }
                // SecureTempFileManager automatically cleans up the file on dispose
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"ReadDepotFile({depotPath})");
                throw new Exception($"Failed to read depot file '{depotPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes content to a depot file
        /// </summary>
        /// <param name="depotPath">Full depot path to the file</param>
        /// <param name="content">Content to write</param>
        /// <param name="description">Changelist description</param>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when file write fails</exception>
        public void WriteDepotFile(string depotPath, string content, string description, bool submitImmediately = true)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty", nameof(description));

            try
            {
                string clientName = _connection?.Client?.Name ?? "unknown";
                _loggingService.LogInfo($"Writing to depot file: {depotPath} using client: {clientName}");

                // Ensure the connection is using the correct client
                if (_connection!.Client != null && !string.IsNullOrWhiteSpace(clientName) && clientName != "unknown")
                {
                    _repository!.Connection.Client = _repository.GetClient(clientName);
                    _loggingService.LogInfo($"Set repository client to: {_repository.Connection.Client.Name}");
                }

                // 1. Resolve Local Path
                string? localPath = null;
                try
                {
                    // Use 'where' command to find local path mapping
                    _loggingService.LogInfo($"Running 'p4 where {depotPath}' with client {clientName}");
                    P4Command cmd = new P4Command(_repository, "where", true, depotPath);
                    var result = cmd.Run();

                    if (result != null && result.TaggedOutput != null && result.TaggedOutput.Count > 0)
                    {
                        // Log all keys for debugging
                        foreach (var key in result.TaggedOutput[0].Keys)
                        {
                            _loggingService.LogInfo($"'where' output [{key}]: {result.TaggedOutput[0][key]}");
                        }

                        // 'path' key usually holds the local path in 'p4 where' output
                        if (result.TaggedOutput[0].ContainsKey("path"))
                        {
                            localPath = result.TaggedOutput[0]["path"];
                        }
                    }
                    else
                    {
                        _loggingService.LogInfo("'p4 where' returned no results.");
                    }
                }
                catch (P4Exception ex)
                {
                    _loggingService.LogInfo($"'p4 where' failed for {depotPath}: {ex.Message}");
                }

                // Fallback: If 'p4 where' failed or returned nothing, try to construct path manually
                // This is useful for new files that might not be in the view yet, or if 'where' is flaky
                if (string.IsNullOrEmpty(localPath))
                {
                    _loggingService.LogInfo("Attempting fallback path resolution...");
                    try 
                    {
                        var client = _repository!.GetClient(clientName);
                        if (client != null && !string.IsNullOrEmpty(client.Root))
                        {
                            // Check if this is a stream client
                            if (!string.IsNullOrEmpty(client.Stream))
                            {
                                // If the depot path starts with the stream path
                                if (depotPath.StartsWith(client.Stream, StringComparison.OrdinalIgnoreCase))
                                {
                                    string relativePath = depotPath.Substring(client.Stream.Length).TrimStart('/', '\\');
                                    localPath = System.IO.Path.Combine(client.Root, relativePath);
                                    _loggingService.LogInfo($"Fallback resolution successful: {localPath}");
                                }
                            }
                            else 
                            {
                                // For classic clients, it's harder, but we can try basic mapping if possible
                                // or just log that we can't do it
                                _loggingService.LogInfo("Fallback failed: Client is not a stream client or logic too complex.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError(ex, "Fallback path resolution error");
                    }
                }

                if (string.IsNullOrEmpty(localPath))
                {
                    throw new Exception($"Could not determine local path for '{depotPath}'. Ensure it is mapped in the current workspace '{clientName}'.");
                }

                _loggingService.LogInfo($"Resolved local path: {localPath}");

                // 2. Prepare Directory
                var directory = System.IO.Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 3. Create Changelist
                var changelist = new Changelist();
                changelist.Description = description;
                changelist = _repository!.CreateChangelist(changelist);

                // 4. Open for Edit/Add
                var fileSpec = new FileSpec(new DepotPath(depotPath), null, null, null);
                bool isOpenForEdit = false;

                // Check if file is already opened (in any changelist)
                bool alreadyOpened = false;
                int existingChangelistId = 0;
                try
                {
                    var checkOpenedCmd = new P4Command(_repository!, "opened", true, depotPath);
                    var checkOpenedResult = checkOpenedCmd.Run();
                    if (checkOpenedResult != null && checkOpenedResult.TaggedOutput != null && checkOpenedResult.TaggedOutput.Count > 0)
                    {
                        alreadyOpened = true;
                        if (checkOpenedResult.TaggedOutput[0].ContainsKey("change"))
                        {
                            int.TryParse(checkOpenedResult.TaggedOutput[0]["change"], out existingChangelistId);
                        }
                        string action = checkOpenedResult.TaggedOutput[0].ContainsKey("action") ? checkOpenedResult.TaggedOutput[0]["action"] : "unknown";
                        _loggingService.LogInfo($"File is already opened for {action} in changelist {existingChangelistId}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogInfo($"Opened check: {ex.Message}");
                }

                // If file is already opened, reopen it into our changelist
                if (alreadyOpened)
                {
                    _loggingService.LogInfo($"Reopening file from changelist {existingChangelistId} to {changelist.Id}...");
                    try
                    {
                        var reopenCmd = new P4Command(_repository!, "reopen", true, "-c", changelist.Id.ToString(), depotPath);
                        var reopenResult = reopenCmd.Run();
                        if (reopenResult != null && reopenResult.TaggedOutput != null && reopenResult.TaggedOutput.Count > 0)
                        {
                            _loggingService.LogInfo($"Successfully reopened file in changelist {changelist.Id}");
                            isOpenForEdit = true;
                        }
                        else
                        {
                            _loggingService.LogInfo("Reopen returned no tagged output, but may have succeeded.");
                            isOpenForEdit = true; // Assume success if no error was thrown
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError(ex, "Reopen failed");
                        throw new Exception($"File is already opened in changelist {existingChangelistId} and could not be reopened: {ex.Message}");
                    }
                }

                // Check if file exists in Perforce (to decide between Add and Edit)
                bool exists = false;
                if (!isOpenForEdit)
                {
                    try
                    {
                        var mdOptions = new GetFileMetaDataCmdOptions(
                            GetFileMetadataCmdFlags.None,
                            null, null, -1, null, null, null
                        );
                        var md = _repository!.GetFileMetaData(new List<FileSpec> { fileSpec }, mdOptions);
                        if (md != null && md.Count > 0 && md[0] != null && 
                            md[0].HeadAction != FileAction.Delete && 
                            md[0].HeadAction != FileAction.MoveDelete)
                        {
                            exists = true;
                            _loggingService.LogInfo($"File exists in depot at revision {md[0].HeadRev}");
                        }
                    }
                    catch (Exception ex)
                    { 
                        _loggingService.LogInfo($"File not found in depot (will add): {ex.Message}");
                    }

                    try 
                    {
                        if (exists)
                        {
                            // Sync the file first to ensure we are editing the latest revision
                            // This prevents "must resolve" errors if we are out of date
                            _loggingService.LogInfo("Syncing file before edit...");
                            _connection!.Client!.SyncFiles(new List<FileSpec> { fileSpec }, null);

                            var editOptions = new EditCmdOptions(EditFilesCmdFlags.None, changelist.Id, null);
                            var editedFiles = _connection.Client.EditFiles(new List<FileSpec> { fileSpec }, editOptions);
                            if (editedFiles != null && editedFiles.Count > 0)
                            {
                                _loggingService.LogInfo($"Successfully opened file for edit: {editedFiles[0].DepotPath}");
                                isOpenForEdit = true;
                            }
                            else
                            {
                                _loggingService.LogInfo("EditFiles returned no results.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogInfo($"EditFiles failed: {ex.Message}. Trying AddFiles.");
                    }
                }

                if (!isOpenForEdit)
                {
                    // For AddFiles to work, the local file must exist first
                    // Create the file with content before attempting to add
                    var addFileDir = System.IO.Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(addFileDir) && !System.IO.Directory.Exists(addFileDir))
                    {
                        System.IO.Directory.CreateDirectory(addFileDir);
                    }
                    
                    _loggingService.LogInfo($"Creating local file before add: {localPath}");
                    System.IO.File.WriteAllText(localPath, content);
                    
                    // Try Add
                    _loggingService.LogInfo("Opening file for add...");
                    var addOptions = new AddFilesCmdOptions(AddFilesCmdFlags.None, changelist.Id, null);
                    var addedFiles = _connection!.Client!.AddFiles(new List<FileSpec> { fileSpec }, addOptions);
                    if (addedFiles == null || addedFiles.Count == 0)
                    {
                        // Provide detailed diagnostic information
                        string diagClientRoot = "unknown";
                        string diagClientStream = "unknown";
                        try
                        {
                            var clientSpec = _repository!.GetClient(clientName);
                            if (clientSpec != null)
                            {
                                diagClientRoot = clientSpec.Root ?? "not set";
                                diagClientStream = clientSpec.Stream ?? "not a stream client";
                            }
                        }
                        catch { }
                        
                        _loggingService.LogError(null, $"Failed to add file. Workspace: {clientName}, Root: {diagClientRoot}, Stream: {diagClientStream}, DepotPath: {depotPath}, LocalPath: {localPath}");
                        throw new Exception($"Failed to open file for add: No files were opened.\n\n" +
                            $"Depot path: {depotPath}\n" +
                            $"Local path: {localPath}\n" +
                            $"Workspace: {clientName}\n" +
                            $"Workspace root: {diagClientRoot}\n" +
                            $"Workspace stream: {diagClientStream}\n\n" +
                            $"The file path may not be mapped in this workspace. " +
                            $"Ensure your workspace is configured for the stream and the path is within the workspace mapping.");
                    }
                    _loggingService.LogInfo($"Successfully opened file for add: {addedFiles[0].DepotPath}");
                }
                else
                {
                    // File was opened for edit, write the content now
                    // Make sure file is writable if it exists (p4 edit should have done this, but to be safe)
                    if (System.IO.File.Exists(localPath))
                    {
                        var fileInfo = new System.IO.FileInfo(localPath);
                        if (fileInfo.IsReadOnly)
                        {
                            fileInfo.IsReadOnly = false;
                        }
                    }

                    _loggingService.LogInfo($"Writing content to local file: {localPath}");
                    System.IO.File.WriteAllText(localPath, content);
                }

                // 6. Verify file is in changelist
                _loggingService.LogInfo($"Verifying file is opened in changelist {changelist.Id}...");
                var openedCmd = new P4Command(_repository!, "opened", true, "-c", changelist.Id.ToString(), depotPath);
                var openedResult = openedCmd.Run();
                
                bool fileIsOpened = false;
                if (openedResult != null && openedResult.TaggedOutput != null && openedResult.TaggedOutput.Count > 0)
                {
                    fileIsOpened = true;
                    string openedFile = openedResult.TaggedOutput[0].ContainsKey("depotFile") ? openedResult.TaggedOutput[0]["depotFile"] : depotPath;
                    _loggingService.LogInfo($"File is opened: {openedFile}");
                }
                
                if (!fileIsOpened)
                {
                    throw new Exception($"File {depotPath} is not opened in changelist {changelist.Id}. Cannot proceed. Check that the file is mapped in your workspace.");
                }
                
                if (submitImmediately)
                {
                    _loggingService.LogInfo($"File verified in changelist {changelist.Id}. Submitting...");
                    
                    // Submit
                    var submitOptions = new SubmitCmdOptions(SubmitFilesCmdFlags.None, changelist.Id, null, null, null);
                    var submitResults = _connection!.Client!.SubmitFiles(submitOptions, null);
                    
                    if (submitResults != null && submitResults.Files != null)
                    {
                        _loggingService.LogInfo($"Successfully submitted {submitResults.Files.Count} file(s)");
                    }
                    else
                    {
                        throw new Exception("Submit returned no results");
                    }
                    
                    _loggingService.LogInfo($"Successfully submitted {depotPath}");
                }
                else
                {
                    _loggingService.LogInfo($"File left pending in changelist {changelist.Id} (submit skipped by user request)");
                }
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"WriteDepotFile({depotPath})");
                throw new Exception($"Failed to write depot file '{depotPath}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"WriteDepotFile({depotPath})");
                throw new Exception($"Failed to write depot file '{depotPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Automatically detects and switches to a workspace that maps the specified stream
        /// </summary>
        /// <param name="streamPath">The stream path to find a workspace for</param>
        /// <returns>True if a workspace was found and switched to, false otherwise</returns>
        public bool AutoDetectAndSwitchWorkspace(string streamPath)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(streamPath))
                return false;

            // Validate stream path for security
            if (!P4InputValidator.ValidateStreamPath(streamPath, out _))
                return false;
                
            try
            {
                _loggingService.LogInfo($"Attempting to auto-detect workspace for stream: {streamPath}");
                
                // Get current user and host
                string currentUser = _connection!.UserName;
                string currentHost = Environment.MachineName;
                
                // Command: p4 clients -u <user> -S <stream>
                var cmd = new P4Command(_repository!, "clients", true, "-u", currentUser, "-S", streamPath);
                var results = cmd.Run();
                
                if (results != null && results.TaggedOutput != null)
                {
                    foreach (var client in results.TaggedOutput)
                    {
                        if (client.ContainsKey("client") && client.ContainsKey("Host"))
                        {
                            string clientName = client["client"];
                            string clientHost = client["Host"];
                            
                            // Check if host matches or is empty (meaning any host)
                            // Note: Perforce host names are case-insensitive
                            if (string.IsNullOrWhiteSpace(clientHost) || 
                                string.Equals(clientHost, currentHost, StringComparison.OrdinalIgnoreCase))
                            {
                                // We found a candidate. Let's verify if its root exists locally.
                                // This helps distinguish between multiple workspaces for the same stream
                                // where one might be stale or on a different drive.
                                try 
                                {
                                    var clientSpec = _repository!.GetClient(clientName);
                                    if (clientSpec != null && !string.IsNullOrWhiteSpace(clientSpec.Root))
                                    {
                                        if (System.IO.Directory.Exists(clientSpec.Root))
                                        {
                                            _loggingService.LogInfo($"Found matching workspace with existing root: {clientName} ({clientSpec.Root})");
                                            _connection!.Client.Name = clientName;
                                            return true;
                                        }
                                        else
                                        {
                                            _loggingService.LogInfo($"Found matching workspace {clientName}, but root '{clientSpec.Root}' does not exist. Skipping.");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _loggingService.LogInfo($"Failed to inspect client '{clientName}': {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // If we are here, we didn't find a client with an existing root.
                    // Fallback: try to pick the first matching one regardless of root existence,
                    // just in case our Directory.Exists check failed for some permissions reason 
                    // or if the user intends to create the root.
                    foreach (var client in results.TaggedOutput)
                    {
                        if (client.ContainsKey("client"))
                        {
                            string clientName = client["client"];
                            string clientHost = client.ContainsKey("Host") ? client["Host"] : "";
                            
                            if (string.IsNullOrWhiteSpace(clientHost) || 
                                string.Equals(clientHost, currentHost, StringComparison.OrdinalIgnoreCase))
                            {
                                _loggingService.LogInfo($"Falling back to workspace: {clientName} (Root check skipped/failed)");
                                _connection.Client.Name = clientName;
                                return true;
                            }
                        }
                    }
                }
                
                _loggingService.LogInfo("No matching workspace found for the current user and host.");
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, $"AutoDetectAndSwitchWorkspace({streamPath})");
                return false;
            }
        }

        /// <summary>
        /// Gets the revision history for a depot file
        /// </summary>
        /// <param name="depotPath">Full depot path to the file</param>
        /// <returns>List of FileRevisionInfo ordered by revision (newest first)</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when file history retrieval fails</exception>
        public List<Models.FileRevisionInfo> GetFileHistory(string depotPath)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            try
            {
                _loggingService.LogInfo($"Getting file history for: {depotPath}");

                // Use 'p4 filelog' command to get file history
                var cmd = new P4Command(_repository!, "filelog", true, "-l", depotPath);
                var results = cmd.Run();
                var revisions = new List<Models.FileRevisionInfo>();

                if (results?.TaggedOutput != null)
                {
                    foreach (var record in results.TaggedOutput)
                    {
                        // Each record in filelog output contains revision info
                        // Look for keys like rev0, change0, action0, time0, user0, desc0
                        // for the first revision, rev1, change1, etc. for subsequent revisions
                        
                        int revIndex = 0;
                        while (record.ContainsKey($"rev{revIndex}"))
                        {
                            var revInfo = new Models.FileRevisionInfo();

                            if (record.TryGetValue($"rev{revIndex}", out var revStr) && int.TryParse(revStr, out var rev))
                                revInfo.Revision = rev;

                            if (record.TryGetValue($"change{revIndex}", out var changeStr) && int.TryParse(changeStr, out var change))
                                revInfo.Changelist = change;

                            if (record.TryGetValue($"time{revIndex}", out var timeStr) && long.TryParse(timeStr, out var unixTime))
                                revInfo.Date = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;

                            if (record.TryGetValue($"user{revIndex}", out var user))
                                revInfo.User = user ?? "";

                            if (record.TryGetValue($"desc{revIndex}", out var desc))
                                revInfo.Description = desc?.Trim() ?? "";

                            if (record.TryGetValue($"action{revIndex}", out var action))
                                revInfo.Action = action ?? "";

                            revisions.Add(revInfo);
                            revIndex++;
                        }
                    }
                }

                _loggingService.LogInfo($"Found {revisions.Count} revisions for {depotPath}");
                return revisions;
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"GetFileHistory({depotPath})");
                throw new Exception($"Failed to get file history for '{depotPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads the content of a depot file at a specific revision
        /// </summary>
        /// <param name="depotPath">Full depot path to the file</param>
        /// <param name="revision">Revision number to read</param>
        /// <returns>File content as string</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected</exception>
        /// <exception cref="ArgumentException">Thrown when depot path is invalid</exception>
        /// <exception cref="Exception">Thrown when file read fails</exception>
        public string ReadDepotFileAtRevision(string depotPath, int revision)
        {
            EnsureConnected();

            // Validate depot path for security
            P4InputValidator.ValidateDepotPathOrThrow(depotPath, nameof(depotPath));

            if (revision < 1)
                throw new ArgumentException("Revision must be >= 1", nameof(revision));

            try
            {
                _loggingService.LogInfo($"Reading depot file: {depotPath}#{revision}");

                // Use 'p4 print -q' to output file content to a secure temp file
                using (var tempFileManager = new SecureTempFileManager())
                {
                    string fileSpec = $"{depotPath}#{revision}";
                    var cmd = new P4Command(_repository!, "print", false, "-q", "-o", tempFileManager.FilePath, fileSpec);
                    var result = cmd.Run();

                    if (result.ErrorList != null && result.ErrorList.Count > 0)
                    {
                        throw new P4Exception(result.ErrorList[0]);
                    }

                    if (System.IO.File.Exists(tempFileManager.FilePath))
                    {
                        return System.IO.File.ReadAllText(tempFileManager.FilePath);
                    }

                    return string.Empty;
                }
                // SecureTempFileManager automatically cleans up the file on dispose
            }
            catch (P4Exception ex)
            {
                _loggingService.LogError(ex, $"ReadDepotFileAtRevision({depotPath}#{revision})");
                throw new Exception($"Failed to read depot file '{depotPath}#{revision}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disposes of the service and disconnects from Perforce
        /// </summary>
        public void Dispose()
        {
            StopSessionTimer();
            Disconnect();
        }
    }
}