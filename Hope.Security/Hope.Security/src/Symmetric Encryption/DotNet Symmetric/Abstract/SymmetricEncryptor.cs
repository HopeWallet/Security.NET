﻿using Hope.Random.Bytes;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Hope.Security.SymmetricEncryption.DotNetSymmetric
{
    /// <summary>
    /// Generic class which implements usability to symmetric encryption algorithms derived from SymmetricAlgorithm.
    /// </summary>
    /// <typeparam name="T"> The type of the class inheriting the SymmetricEncryptor. </typeparam>
    /// <typeparam name="A"> The derived type of the SymmetricAlgorithm. </typeparam>
    public abstract class SymmetricEncryptor<T, A> : AdvancedEntropyEncryptor
        where T : SymmetricEncryptor<T, A>, new()
        where A : SymmetricAlgorithm, new()
    {
        private const int ITERATIONS = 1000;

        /// <summary>
        /// The key size and block size to use during the encryption.
        /// </summary>
        protected abstract int KeySize { get; }

        /// <summary>
        /// The salt and iv byte size to use during the encryption.
        /// </summary>
        protected abstract int SaltIvByteSize { get; }

        /// <summary>
        /// Initializes the <see cref="SymmetricEncryptor"/> by assigning all additional encryptors to encrypt the data with.
        /// </summary>
        /// <param name="encryptors"> The encryptors to encrypt/decrypt data with. </param>
        protected SymmetricEncryptor(params object[] encryptors) : base(encryptors)
        {
        }

        /// <summary>
        /// Encrypts <see langword="byte"/>[] data using the <see cref="SymmetricAlgorithm"/> of the derived class.
        /// </summary>
        /// <param name="data"> The <see langword="byte"/>[] data to encrypt. </param>
        /// <param name="entropy"> The additional entropy to apply to the encryption. </param>
        /// <returns> The encrypted <see langword="byte"/>[] data. </returns>
        public override byte[] Encrypt(byte[] data, byte[] entropy) => InternalEncrypt(data, GetAdvancedEntropyHash(entropy), ITERATIONS);

        /// <summary>
        /// Decrypts encrypted <see langword="byte"/>[] data.
        /// </summary>
        /// <param name="encryptedData"> The encrypted <see langword="byte"/>[] data. </param>
        /// <param name="entropy"> The additional entropy to apply to the decryption. </param>
        /// <returns> The decrypted <see langword="byte"/>[] data. </returns>
        public override byte[] Decrypt(byte[] encryptedData, byte[] entropy) => InternalDecrypt(encryptedData, GetAdvancedEntropyHash(entropy), ITERATIONS);

        /// <summary>
        /// Encrypts <see langword="byte"/>[] data using the <see cref="SymmetricAlgorithm"/> provided.
        /// </summary>
        /// <param name="data"> The <see langword="byte"/>[] data to encrypt. </param>
        /// <param name="entropy"> The entropy to use to encrypt the data. </param>
        /// <param name="iterations"> The number of iterations to apply to <see cref="Rfc2898DeriveBytes"/> to derive the encryption key. </param>
        /// <returns> The encrypted <see langword="byte"/>[] data. </returns>
        private byte[] InternalEncrypt(byte[] data, byte[] entropy, int iterations)
        {
            byte[] saltStringBytes = RandomBytes.Secure.SHA3.GetBytes(SaltIvByteSize);
            byte[] ivStringBytes = RandomBytes.Secure.SHA3.GetBytes(SaltIvByteSize);

            using (var password = new Rfc2898DeriveBytes(entropy, saltStringBytes, iterations))
            using (var symmetricKey = new A())
            {
                symmetricKey.BlockSize = KeySize;
                symmetricKey.Mode = CipherMode.CBC;
                symmetricKey.Padding = PaddingMode.PKCS7;

                using (var encryptor = symmetricKey.CreateEncryptor(password.GetBytes(KeySize / 8), ivStringBytes))
                using (var memoryStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();

                    ClearEncryptionStreams(symmetricKey, memoryStream, cryptoStream);

                    return saltStringBytes.Concat(ivStringBytes).ToArray().Concat(memoryStream.ToArray()).ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts <see langword="byte"/>[] data using the <see cref="SymmetricAlgorithm"/> provided.
        /// </summary>
        /// <param name="encryptedData"> The <see langword="byte"/>[] data to decrypt. </param>
        /// <param name="entropy"> The entropy to use to decrypt the data. </param>
        /// <param name="iterations"> The number of iterations to apply to <see cref="Rfc2898DeriveBytes"/> to derive the decryption key. </param>
        /// <returns> The decrypted <see langword="byte"/>[] data. </returns>
        private byte[] InternalDecrypt(byte[] encryptedData, byte[] entropy, int iterations)
        {
            byte[] saltStringBytes = encryptedData.Take(KeySize / 8).ToArray();
            byte[] ivStringBytes = encryptedData.Skip(KeySize / 8).Take(KeySize / 8).ToArray();
            byte[] cipherTextBytes = encryptedData.Skip((KeySize / 8) * 2).Take(encryptedData.Length - ((KeySize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(entropy, saltStringBytes, iterations))
            using (var symmetricKey = new A())
            {
                symmetricKey.BlockSize = KeySize;
                symmetricKey.Mode = CipherMode.CBC;
                symmetricKey.Padding = PaddingMode.PKCS7;

                using (var decryptor = symmetricKey.CreateDecryptor(password.GetBytes(KeySize / 8), ivStringBytes))
                using (var memoryStream = new MemoryStream(cipherTextBytes))
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    byte[] decryptedData = new byte[cryptoStream.Read(cipherTextBytes, 0, cipherTextBytes.Length)];

                    ClearEncryptionStreams(symmetricKey, memoryStream, cryptoStream);

                    Array.Copy(cipherTextBytes, decryptedData, decryptedData.Length);

                    return decryptedData;
                }
            }
        }

        /// <summary>
        /// Clears out all encryption streams.
        /// </summary>
        /// <param name="symmetricAlgorithm"> The <see cref="SymmetricAlgorithm"/> to clear all data for. </param>
        /// <param name="memoryStream"> The used <see cref="MemoryStream"/> to clear. </param>
        /// <param name="cryptoStream"> The used <see cref="CryptoStream"/> to clear. </param>
        private void ClearEncryptionStreams(A symmetricAlgorithm, MemoryStream memoryStream, CryptoStream cryptoStream)
        {
            symmetricAlgorithm.Clear();
            memoryStream.Close();
            cryptoStream.Close();
        }
    }
}
