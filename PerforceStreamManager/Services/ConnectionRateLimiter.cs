using System;
using System.IO;
using System.Text.Json;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Implements connection rate limiting with exponential backoff to prevent brute force attacks.
    /// Persists state to prevent bypassing by application restart.
    /// </summary>
    public class ConnectionRateLimiter
    {
        private int _failedAttempts = 0;
        private DateTime _lastAttempt = DateTime.MinValue;
        private TimeSpan _lockoutDuration = TimeSpan.Zero;
        private readonly string _statePath;
        private const int MaxFailedAttempts = 3;
        private const int BaseBackoffSeconds = 5;

        /// <summary>
        /// Initializes a new instance of ConnectionRateLimiter.
        /// </summary>
        public ConnectionRateLimiter()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "PerforceStreamManager");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _statePath = Path.Combine(appFolder, "rate_limit_state.json");

            LoadState();
        }

        /// <summary>
        /// Checks if a connection attempt is allowed based on rate limiting rules.
        /// </summary>
        /// <param name="waitTime">Time to wait before next attempt is allowed</param>
        /// <returns>True if connection can be attempted, false if rate limited</returns>
        public bool CanAttemptConnection(out TimeSpan waitTime)
        {
            waitTime = TimeSpan.Zero;

            if (_failedAttempts >= MaxFailedAttempts)
            {
                var timeSinceLastAttempt = DateTime.Now - _lastAttempt;
                if (timeSinceLastAttempt < _lockoutDuration)
                {
                    waitTime = _lockoutDuration - timeSinceLastAttempt;
                    return false;
                }
                else
                {
                    // Lockout period has passed, reset but keep one failed attempt
                    // to prevent immediate retry loops
                    _failedAttempts = 1;
                    _lockoutDuration = TimeSpan.FromSeconds(BaseBackoffSeconds);
                }
            }

            return true;
        }

        /// <summary>
        /// Records a failed connection attempt and updates lockout duration.
        /// </summary>
        public void RecordFailedAttempt()
        {
            _failedAttempts++;
            _lastAttempt = DateTime.Now;

            // Exponential backoff: 5s, 15s, 45s, 135s...
            _lockoutDuration = TimeSpan.FromSeconds(BaseBackoffSeconds * Math.Pow(3, _failedAttempts - 1));

            SaveState();
        }

        /// <summary>
        /// Records a successful connection and resets the rate limiter.
        /// </summary>
        public void RecordSuccessfulConnection()
        {
            _failedAttempts = 0;
            _lockoutDuration = TimeSpan.Zero;
            _lastAttempt = DateTime.MinValue;

            SaveState();
        }

        /// <summary>
        /// Gets the number of failed attempts.
        /// </summary>
        public int FailedAttempts => _failedAttempts;

        /// <summary>
        /// Gets the current lockout duration.
        /// </summary>
        public TimeSpan LockoutDuration => _lockoutDuration;

        /// <summary>
        /// Manually resets the rate limiter (useful for admin override).
        /// </summary>
        public void Reset()
        {
            RecordSuccessfulConnection();
        }

        /// <summary>
        /// Saves the current state to disk to persist across application restarts.
        /// </summary>
        private void SaveState()
        {
            try
            {
                var state = new RateLimitState
                {
                    FailedAttempts = _failedAttempts,
                    LastAttempt = _lastAttempt,
                    LockoutDurationSeconds = _lockoutDuration.TotalSeconds
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statePath, json);
            }
            catch
            {
                // Ignore errors saving state - rate limiting will still work for current session
            }
        }

        /// <summary>
        /// Loads the persisted state from disk.
        /// </summary>
        private void LoadState()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    string json = File.ReadAllText(_statePath);
                    var state = JsonSerializer.Deserialize<RateLimitState>(json);

                    if (state != null)
                    {
                        _failedAttempts = state.FailedAttempts;
                        _lastAttempt = state.LastAttempt;
                        _lockoutDuration = TimeSpan.FromSeconds(state.LockoutDurationSeconds);

                        // If the last attempt was more than 24 hours ago, reset
                        if (DateTime.Now - _lastAttempt > TimeSpan.FromHours(24))
                        {
                            Reset();
                        }
                    }
                }
            }
            catch
            {
                // If we can't load state, start fresh
                Reset();
            }
        }

        /// <summary>
        /// Internal class for serializing rate limit state.
        /// </summary>
        private class RateLimitState
        {
            public int FailedAttempts { get; set; }
            public DateTime LastAttempt { get; set; }
            public double LockoutDurationSeconds { get; set; }
        }
    }
}
