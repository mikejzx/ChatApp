using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Mikejzx.ChatClient
{
    public class CryptoCipher
    {
        // Initialisation vector.
        private byte[] m_IV;
        public byte[] IV { get => m_IV; }
        public string IVString { get => Convert.ToBase64String(m_IV); }

        // Encrypted message bytes.
        private byte[] m_Cipher;
        public byte[] Cipher { get => m_Cipher; }
        public string CipherString { get => Convert.ToBase64String(m_Cipher); }

        // Initialise with raw bytes.
        public CryptoCipher(byte[] iv, byte[] cipher)
        {
            this.m_IV = iv;
            this.m_Cipher = cipher;
        }

        // Initialise using base64-encoded strings.
        public CryptoCipher(string ivString, string cipherString)
        {
            this.m_IV = Convert.FromBase64String(ivString);
            this.m_Cipher = Convert.FromBase64String(cipherString);
        }
    }

    public static class ChatClientCrypto
    {
        // Our salt is constant for now.
        public static readonly byte[] Salt = new byte[] { 0x01, 0x02, 0x03, 0x04,
                                                          0x05, 0x06, 0x07, 0x08,
                                                          0x09, 0x0a, 0x0b, 0x0c,
                                                          0x0d, 0x0e, 0x0f, 0x10 };

        // Constant salt as base64 string.
        public static string SaltString { get => Convert.ToBase64String(Salt); }

        // Derive a private key from given password string.
        public static byte[] DeriveKey(string password, byte[] salt)
        {
            // Generate secret key from the password via PBKDF2.
            return Rfc2898DeriveBytes.Pbkdf2(password: Encoding.UTF8.GetBytes(password),
                                             salt: salt,
                                             iterations: 8,
                                             hashAlgorithm: HashAlgorithmName.SHA512,
                                             outputLength: 128 / 8);
        }

        // Encrypt the given message
        public static CryptoCipher EncryptMessage(string plainText, byte[] key)
        {
            byte[] iv;
            byte[] cipher;

            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.BlockSize = 128; // bits
                aes.Key = key;

                iv = aes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor();

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                        cipher = ms.ToArray();
                    }
                }
            }

            return new CryptoCipher(iv, cipher);
        }

        // Decrypt the given message
        public static string DecryptMessage(CryptoCipher cipher, byte[] key, out bool failed)
        {
            string plainText;

            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.BlockSize = 128; // bits
                aes.IV = cipher.IV;
                aes.Key = key;

                ICryptoTransform decryptor = aes.CreateDecryptor();

                // Decrypt the message
                try
                {
                    using (MemoryStream ms = new MemoryStream(cipher.Cipher))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs))
                            {
                                // Use the deciphered message
                                plainText = sr.ReadToEnd();
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Failure; presumably the key is wrong.
                    plainText = "!! failed to decrypt !!";
                    failed = true;
                }
            }

            failed = false;
            return plainText;
        }
    }
}
