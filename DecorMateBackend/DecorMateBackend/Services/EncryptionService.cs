using System.Security.Cryptography;
using System.Text;

namespace DecorMateBackend.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService(IConfiguration configuration)
        {
            // The key is stored as Base64, we need to decode it to get the 32 bytes for AES-256
            var keyString = configuration["Encryption:Key"] ?? throw new InvalidOperationException("Encryption Key is missing in configuration.");
            _key = Convert.FromBase64String(keyString);
            
            if (_key.Length != 32)
            {
                throw new InvalidOperationException($"Encryption key must be 32 bytes (256 bits) for AES-256. Current length: {_key.Length} bytes.");
            }
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.GenerateIV();

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    // Prepend IV to the stream so we can use it for decryption
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;

                // Extract IV from the beginning
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
