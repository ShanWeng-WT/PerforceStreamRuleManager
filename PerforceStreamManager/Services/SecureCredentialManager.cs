using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace PerforceStreamManager.Services
{
    /// <summary>
    /// Manages secure encryption and decryption of credentials using Windows DPAPI.
    /// Uses DataProtectionScope.CurrentUser so only the current user can decrypt.
    /// </summary>
    public static class SecureCredentialManager
    {
        // Marker prefix to identify encrypted passwords vs legacy plaintext
        private const string EncryptedPrefix = "DPAPI:";

        /// <summary>
        /// Encrypts a password using Windows DPAPI (CurrentUser scope).
        /// </summary>
        /// <param name="password">Plaintext password to encrypt</param>
        /// <returns>Base64-encoded encrypted string with prefix marker</returns>
        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(password);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                // Clear the plaintext bytes from memory
                Array.Clear(plainBytes, 0, plainBytes.Length);

                return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to encrypt password using DPAPI.", ex);
            }
        }

        /// <summary>
        /// Encrypts a SecureString password using Windows DPAPI.
        /// </summary>
        /// <param name="securePassword">SecureString password to encrypt</param>
        /// <returns>Base64-encoded encrypted string with prefix marker</returns>
        public static string EncryptPassword(SecureString securePassword)
        {
            if (securePassword == null || securePassword.Length == 0)
                return string.Empty;

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToBSTR(securePassword);
                string plaintext = Marshal.PtrToStringBSTR(ptr);
                return EncryptPassword(plaintext);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ptr);
                }
            }
        }

        /// <summary>
        /// Decrypts a password that was encrypted using EncryptPassword.
        /// Supports automatic migration of legacy plaintext passwords.
        /// </summary>
        /// <param name="encryptedPassword">Encrypted password string (or legacy plaintext)</param>
        /// <returns>Decrypted plaintext password</returns>
        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return string.Empty;

            // Check if this is a legacy plaintext password (not encrypted)
            if (!encryptedPassword.StartsWith(EncryptedPrefix))
            {
                // Return as-is for migration - the settings service will re-encrypt on save
                return encryptedPassword;
            }

            try
            {
                string base64 = encryptedPassword.Substring(EncryptedPrefix.Length);
                byte[] encryptedBytes = Convert.FromBase64String(base64);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                string result = Encoding.UTF8.GetString(decryptedBytes);

                // Clear the decrypted bytes from memory
                Array.Clear(decryptedBytes, 0, decryptedBytes.Length);

                return result;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Failed to decrypt password. The password may have been encrypted by a different user account.", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Invalid encrypted password format.", ex);
            }
        }

        /// <summary>
        /// Decrypts an encrypted password directly into a SecureString.
        /// More secure as the plaintext never exists as a managed string.
        /// </summary>
        /// <param name="encryptedPassword">Encrypted password string</param>
        /// <returns>SecureString containing the decrypted password</returns>
        public static SecureString DecryptToSecureString(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return new SecureString();

            // Check if this is a legacy plaintext password
            if (!encryptedPassword.StartsWith(EncryptedPrefix))
            {
                // Convert legacy plaintext to SecureString
                return ToSecureString(encryptedPassword);
            }

            try
            {
                string base64 = encryptedPassword.Substring(EncryptedPrefix.Length);
                byte[] encryptedBytes = Convert.FromBase64String(base64);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                var secureString = new SecureString();
                try
                {
                    // Convert bytes to chars and append to SecureString
                    char[] chars = Encoding.UTF8.GetChars(decryptedBytes);
                    try
                    {
                        foreach (char c in chars)
                        {
                            secureString.AppendChar(c);
                        }
                    }
                    finally
                    {
                        // Clear char array from memory
                        Array.Clear(chars, 0, chars.Length);
                    }
                }
                finally
                {
                    // Clear decrypted bytes from memory
                    Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
                }

                secureString.MakeReadOnly();
                return secureString;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Failed to decrypt password. The password may have been encrypted by a different user account.", ex);
            }
        }

        /// <summary>
        /// Converts a plain string to a SecureString.
        /// </summary>
        /// <param name="plainText">Plain text to convert</param>
        /// <returns>SecureString containing the text</returns>
        public static SecureString ToSecureString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return new SecureString();

            var secureString = new SecureString();
            foreach (char c in plainText)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        /// <summary>
        /// Converts a SecureString to a plain string.
        /// Use sparingly and clear the result as soon as possible.
        /// </summary>
        /// <param name="secureString">SecureString to convert</param>
        /// <returns>Plain text string</returns>
        public static string ToPlainString(SecureString secureString)
        {
            if (secureString == null || secureString.Length == 0)
                return string.Empty;

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToBSTR(secureString);
                return Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ptr);
                }
            }
        }

        /// <summary>
        /// Checks if a stored password is already encrypted.
        /// </summary>
        /// <param name="storedPassword">Password from storage</param>
        /// <returns>True if encrypted, false if plaintext (legacy)</returns>
        public static bool IsEncrypted(string? storedPassword)
        {
            return storedPassword != null && storedPassword.StartsWith(EncryptedPrefix);
        }
    }
}
