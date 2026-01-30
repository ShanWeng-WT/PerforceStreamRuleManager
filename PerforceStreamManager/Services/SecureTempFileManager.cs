using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Manages secure temporary files with proper permissions and cleanup.
    /// Implements IDisposable to ensure temp files are properly deleted.
    /// </summary>
    public class SecureTempFileManager : IDisposable
    {
        private readonly string _tempFilePath;
        private bool _disposed;

        /// <summary>
        /// Gets the path to the secure temporary file.
        /// </summary>
        public string FilePath => _tempFilePath;

        /// <summary>
        /// Creates a new secure temporary file with restricted permissions.
        /// The file is only accessible by the current user.
        /// </summary>
        /// <param name="extension">Optional file extension (default: .tmp)</param>
        public SecureTempFileManager(string extension = ".tmp")
        {
            // Generate unique filename using GUID to prevent prediction
            string fileName = $"p4_{Guid.NewGuid()}{extension}";
            _tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

            // Create the file with restrictive permissions
            CreateSecureFile();
        }

        /// <summary>
        /// Creates the temporary file with access restricted to current user only.
        /// </summary>
        private void CreateSecureFile()
        {
            try
            {
                // Create an empty file first
                using (var fs = File.Create(_tempFilePath))
                {
                    // File created and closed
                }

                // Set restrictive ACL permissions on Windows
                SetRestrictivePermissions();
            }
            catch (Exception)
            {
                // If secure creation fails, clean up and rethrow
                TryDelete();
                throw;
            }
        }

        /// <summary>
        /// Sets restrictive file permissions so only the current user can access the file.
        /// </summary>
        private void SetRestrictivePermissions()
        {
            try
            {
                var fileInfo = new FileInfo(_tempFilePath);

                // Get the current file security
                var fileSecurity = fileInfo.GetAccessControl();

                // Protect the ACL from inheritance and remove inherited rules
                fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                // Remove all existing access rules
                var existingRules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule rule in existingRules)
                {
                    fileSecurity.RemoveAccessRule(rule);
                }

                // Add full control only for the current user
                var currentUser = WindowsIdentity.GetCurrent();
                if (currentUser.User != null)
                {
                    var rule = new FileSystemAccessRule(
                        currentUser.User,
                        FileSystemRights.FullControl,
                        AccessControlType.Allow
                    );
                    fileSecurity.AddAccessRule(rule);
                }

                // Apply the new security settings
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch (PlatformNotSupportedException)
            {
                // ACL not supported on this platform (non-Windows)
                // File is still created with default permissions
            }
            catch (UnauthorizedAccessException)
            {
                // Unable to modify permissions - file still usable but less secure
            }
        }

        /// <summary>
        /// Attempts to securely delete the temporary file.
        /// </summary>
        private void TryDelete()
        {
            try
            {
                if (File.Exists(_tempFilePath))
                {
                    // Overwrite with zeros before deletion for additional security
                    // (Optional - adds security but costs performance)
                    // SecureOverwrite();

                    File.Delete(_tempFilePath);
                }
            }
            catch
            {
                // Best effort cleanup - don't throw during dispose
            }
        }

        /// <summary>
        /// Overwrites file content with zeros before deletion.
        /// Provides additional security for sensitive data.
        /// </summary>
        private void SecureOverwrite()
        {
            try
            {
                var fileInfo = new FileInfo(_tempFilePath);
                if (fileInfo.Exists && fileInfo.Length > 0)
                {
                    // Make file writable if needed
                    if (fileInfo.IsReadOnly)
                    {
                        fileInfo.IsReadOnly = false;
                    }

                    // Overwrite with zeros
                    using (var fs = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Write))
                    {
                        var zeros = new byte[Math.Min(fileInfo.Length, 8192)];
                        long remaining = fileInfo.Length;
                        while (remaining > 0)
                        {
                            int toWrite = (int)Math.Min(remaining, zeros.Length);
                            fs.Write(zeros, 0, toWrite);
                            remaining -= toWrite;
                        }
                        fs.Flush();
                    }
                }
            }
            catch
            {
                // Best effort - continue with normal deletion
            }
        }

        /// <summary>
        /// Disposes of the temporary file manager and deletes the temp file.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                TryDelete();
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup if Dispose is not called.
        /// </summary>
        ~SecureTempFileManager()
        {
            Dispose(false);
        }
    }
}
