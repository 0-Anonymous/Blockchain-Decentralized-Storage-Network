using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeezFiles.Utilities
{
    /// <summary>
    /// Provides helper methods for AES encryption and decryption.
    /// </summary>
    public static class CryptHelper
    {
        public static string GenerateMasterKey(int byteLength)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                string keyBase64 = Convert.ToBase64String(aes.Key);
                string ivBase64 = Convert.ToBase64String(aes.IV);

                string content = $"{keyBase64};{ivBase64}";
                return content;
            }
        }

        public static string GenerateMasterKeyFromPassword(string username, string password)
        {
            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes($"DNStore:{username.Trim().ToLowerInvariant()}"));
            using var kdf = new Rfc2898DeriveBytes(password, salt, 150_000, HashAlgorithmName.SHA256);
            byte[] key = kdf.GetBytes(32);
            byte[] iv = kdf.GetBytes(16);
            return $"{Convert.ToBase64String(key)};{Convert.ToBase64String(iv)}";
        }

        public static void SavePasswordDerivedMasterKey(string username, string password, string configPath)
        {
            Directory.CreateDirectory(configPath);
            File.WriteAllText(Path.Combine(configPath, "secret.txt"), GenerateMasterKeyFromPassword(username, password));
        }

        /// <summary>
        /// Encrypts byte data using AES.
        /// </summary>
        public static byte[] EncryptData(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                        cryptoStream.FlushFinalBlock();
                        return memoryStream.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts byte data using AES.
        /// </summary>
        /// <returns>The decrypted data, or null if decryption fails.</returns>
        public static byte[] DecryptData(byte[] data, byte[] key, byte[] iv)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(data, 0, data.Length);
                            cryptoStream.FlushFinalBlock();
                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine($"[CryptHelper] Decryption failed: {ex.Message}.");
                return null;
            }
        }
    }
}
