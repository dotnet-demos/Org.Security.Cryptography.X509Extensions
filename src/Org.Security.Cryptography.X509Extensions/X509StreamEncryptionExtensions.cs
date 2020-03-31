﻿
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Org.Security.Cryptography
{
    /// <summary>
    /// Extensions to encrypt/decrypt Streams using X509 Certificates.
    /// By default, uses AES-256/128 for Data encryption.
    /// </summary>
    public static class X509StreamEncryptionExtensions
    {
        // Defaults
        const string    DEF_AlgName     = "Aes";
        const int       DEF_KeySize     = 256;
        const int       DEF_BlockSize   = 128;

        // Random prefix, for a private slice of X509 Cache.
        static readonly string X509CachePrefix = Guid.NewGuid().ToString();

        /// <summary>
        /// X509Certificate public key serves as KeyEncryptionKey (KEK).
        /// Data is encrypted using a randomly generated DataEncryptionKey and IV.
        /// Writes encrypted DataEncryptionKey (using KEK), encrypted IV (using KEK) and encrypted data (using DEK) to the output stream.
        /// </summary>
        public static void Encrypt(this Stream inputStream, Stream outputStream, string thumbPrint, StoreName storeName, StoreLocation storeLocation, string algName = DEF_AlgName, int keySize = DEF_KeySize, int blockSize = DEF_BlockSize)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == thumbPrint) throw new ArgumentNullException(nameof(thumbPrint));
            if (null == algName) throw new ArgumentNullException(nameof(algName));

            Encrypt(
                inputStream, outputStream,
                X509CertificateCache.GetCertificate(thumbPrint, storeName, storeLocation, X509CachePrefix),
                algName: algName, keySize: keySize, blockSize: blockSize
            );
        }

        /// <summary>
        /// X509Certificate private key serves as KeyEncryptionKey (KEK).
        /// Reads and decrypts the Encrypted DataEncryptionKey and Encrypted IV using the KEK.
        /// Decrypts the data using the DataEncryptionKey and IV. 
        /// </summary>
        public static void Decrypt(this Stream inputStream, Stream outputStream, string thumbPrint, StoreName storeName, StoreLocation storeLocation, string algName = DEF_AlgName)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == thumbPrint) throw new ArgumentNullException(nameof(thumbPrint));
            if (null == algName) throw new ArgumentNullException(nameof(algName));

            Decrypt(
                inputStream, outputStream,
                X509CertificateCache.GetCertificate(thumbPrint, storeName, storeLocation, X509CachePrefix),
                algName
            );
        }

        /// <summary>
        /// X509Certificate public key serves as KeyEncryptionKey (KEK).
        /// Data is encrypted using a randomly generated DataEncryptionKey and IV.
        /// Writes encrypted DataEncryptionKey (using KEK), encrypted IV (using KEK) and encrypted data (using DEK) to the output stream.
        /// </summary>
        public static void Encrypt(this Stream inputStream, Stream outputStream, X509Certificate2 cert, string algName = DEF_AlgName, int keySize = DEF_KeySize, int blockSize = DEF_BlockSize)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == cert) throw new ArgumentNullException(nameof(cert));
            if (null == algName) throw new ArgumentNullException(nameof(algName));

            // Encrypt using Public key.
            // DO NOT Dispose this; Doing so will render the X509Certificate in the cache use-less.
            // Did endurance test of 1 mil cycles, found NO HANDLE leak.
            var keyEncryption = cert.GetRsaPublicKeyAsymmetricAlgorithm();

            using (var dataEncryption = SymmetricAlgorithm.Create(algName))
            {
                if (null == dataEncryption) throw new Exception($"SymmetricAlgorithm.Create() returned null. Check algName: '{algName}'");

                // Select suggested keySize/blockSize.
                dataEncryption.KeySize = keySize;
                dataEncryption.BlockSize = blockSize;
                Encrypt(inputStream, outputStream, keyEncryption,  dataEncryption);
            }
        }

        /// <summary>
        /// X509Certificate private key serves as KeyEncryptionKey (KEK).
        /// Reads and decrypts the Encrypted DataEncryptionKey and Encrypted IV using the KEK.
        /// Decrypts the data using the DataEncryptionKey and IV. 
        /// </summary>
        public static void Decrypt(this Stream inputStream, Stream outputStream, X509Certificate2 cert, string algName = DEF_AlgName)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == cert) throw new ArgumentNullException(nameof(cert));
            if (null == algName) throw new ArgumentNullException(nameof(algName));

            // Decrypt using Private key.
            // DO NOT Dispose this; Doing so will render the X509Certificate in cache use-less.
            // Did endurance test of 1 mil cycles, found NO HANDLE leak.
            var keyEncryption = cert.GetRsaPrivateKeyAsymmetricAlgorithm();

            using (var dataEncryption = SymmetricAlgorithm.Create(algName))
            {
                if (null == dataEncryption) throw new Exception($"SymmetricAlgorithm.Create() returned null. Check algName: '{algName}'");

                // KeySize/blockSize will be selected when we assign key/IV later.
                Decrypt(inputStream, outputStream, keyEncryption, dataEncryption);
            }
        }

        //...............................................................................
        #region Encrypt/Decrypt the Key (Asymmetric) and the Data (Symmetric)
        //...............................................................................

        static void Encrypt(Stream inputStream, Stream outputStream, AsymmetricAlgorithm keyEncryption, SymmetricAlgorithm dataEncryption)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == keyEncryption) throw new ArgumentNullException(nameof(keyEncryption));
            if (null == dataEncryption) throw new ArgumentNullException(nameof(dataEncryption));

            // About...
            // Trace.WriteLine($"Encrypting. KEK: {keyEncryption.GetType().Name} / {keyEncryption.KeySize} bits");
            // Trace.WriteLine($"Encrypting. DEK: {dataEncryption.GetType().Name} / {dataEncryption.KeySize} bits / BlockSize: {dataEncryption.BlockSize} bits");

            // The DataEncryptionKey and the IV.
            var DEK = dataEncryption.Key ?? throw new Exception("SymmetricAlgorithm.Key was NULL");
            var IV = dataEncryption.IV ?? throw new Exception("SymmetricAlgorithm.IV was NULL");

            // Encrypt the DataEncryptionKey and the IV
            var keyFormatter = new RSAPKCS1KeyExchangeFormatter(keyEncryption);
            var encryptedDEK = keyFormatter.CreateKeyExchange(DEK);
            var encryptedIV = keyFormatter.CreateKeyExchange(IV);

            // Write the Encrypted DEK and IV
            outputStream.WriteLengthAndBytes(encryptedDEK);
            outputStream.WriteLengthAndBytes(encryptedIV);

            // Write the encrypted data.
            // Note: Disposing the CryptoStream also disposes the outputStream. There is no keepOpen option.
            using (var transform = dataEncryption.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(outputStream, transform, CryptoStreamMode.Write))
            {
                inputStream.CopyTo(cryptoStream, bufferSize: dataEncryption.BlockSize * 4);
            }
        }

        static void Decrypt(Stream inputStream, Stream outputStream, AsymmetricAlgorithm keyEncryption, SymmetricAlgorithm dataEncryption)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == keyEncryption) throw new ArgumentNullException(nameof(keyEncryption));
            if (null == dataEncryption) throw new ArgumentNullException(nameof(dataEncryption));

            var encryptedDEK = inputStream.ReadLengthAndBytes(maxBytes: 2048);
            var encryptedIV = inputStream.ReadLengthAndBytes(maxBytes: 2048);

            var keyDeformatter = new RSAPKCS1KeyExchangeDeformatter(keyEncryption);
            dataEncryption.Key = keyDeformatter.DecryptKeyExchange(encryptedDEK);
            dataEncryption.IV = keyDeformatter.DecryptKeyExchange(encryptedIV);

            // About...
            // Trace.WriteLine($"Decrypting. KEK: {keyEncryption.GetType().Name} / {keyEncryption.KeySize} bits");
            // Trace.WriteLine($"Decrypting. DEK: {dataEncryption.GetType().Name} / {dataEncryption.KeySize} bits / BlockSize: {dataEncryption.BlockSize} bits");

            // Read the encrypted data.
            // Note: Disposing the CryptoStream also disposes the inputStream. There is no keepOpen option.
            using (var transform = dataEncryption.CreateDecryptor())
            using (var cryptoStream = new CryptoStream(inputStream, transform, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(outputStream, bufferSize: dataEncryption.BlockSize * 4);
            }
        }

        #endregion

        //...............................................................................
        #region Utils: WriteLengthAndBytes(), ReadLengthAndBytes()
        //...............................................................................
        static void WriteLengthAndBytes(this Stream outputStream, byte[] bytes)
        {
            if (null == outputStream) throw new ArgumentNullException(nameof(outputStream));
            if (null == bytes) throw new ArgumentNullException(nameof(bytes));

            // Int32 length to exactly-four-bytes array.
            var length = BitConverter.GetBytes((Int32)bytes.Length);

            // Write the four-byte-length followed by the data.
            outputStream.Write(length, 0, length.Length);
            outputStream.Write(bytes, 0, bytes.Length);
        }

        static byte[] ReadLengthAndBytes(this Stream inputStream, int maxBytes)
        {
            if (null == inputStream) throw new ArgumentNullException(nameof(inputStream));

            // Read an Int32, exactly four bytes.
            var arrLength = new byte[4];
            var bytesRead = inputStream.Read(arrLength, 0, 4);
            if (bytesRead != 4) throw new Exception("Unexpected end of InputStream. Expecting 4 bytes.");

            // Length of data to read.
            var length = BitConverter.ToInt32(arrLength, 0);
            if (length > maxBytes) throw new Exception($"Unexpected data size {length:#,0} bytes. Expecting NOT more than {maxBytes:#,0} bytes.");

            // Read suggested no of bytes...
            var bytes = new byte[length];
            bytesRead = inputStream.Read(bytes, 0, bytes.Length);
            if (bytesRead != bytes.Length) throw new Exception($"Unexpected end of input stream. Expecting {bytes.Length:#,0} bytes.");

            return bytes;
        }

        #endregion

        //...............................................................................
        #region Obtain public/private key AsymmetricAlgorithm
        //...............................................................................

        static AsymmetricAlgorithm GetRsaPublicKeyAsymmetricAlgorithm(this X509Certificate2 cert)
        {
            if (null == cert) throw new ArgumentNullException(nameof(cert));
            if (null == cert.Thumbprint) throw new ArgumentNullException("X509Certificate2.Thumbprint was NULL.");

            try
            {
                try
                {
                    // [FASTER] 
                    return cert.PublicKey?.Key ?? throw new Exception($"X509Certificate2.PublicKey?.Key was NULL.");
                }
                catch (CryptographicException)
                {
                    // [SLOWER] 
                    return cert.GetRSAPublicKey() ?? throw new Exception($"X509Certificate2.GetRSAPublicKey() returned NULL");
                }
            }
            catch (Exception err)
            {
                var msg = $"Error accessing PublicKey of the X509 Certificate. Cert: {cert.Thumbprint}";
                throw new Exception(msg, err);
            }
        }

        static AsymmetricAlgorithm GetRsaPrivateKeyAsymmetricAlgorithm(this X509Certificate2 cert)
        {
            if (null == cert) throw new ArgumentNullException(nameof(cert));
            if (null == cert.Thumbprint) throw new ArgumentNullException("X509Certificate2.Thumbprint was NULL.");

            try
            {
                try
                {
                    // [FASTER] 
                    return cert.PrivateKey ?? throw new Exception($"X509Certificate2.PrivateKey was NULL.");
                }
                catch (CryptographicException)
                {
                    // [SLOWER] 
                    return cert.GetRSAPrivateKey() ?? throw new Exception($"X509Certificate2.GetRSAPrivateKey() returned NULL.");
                }
            }
            catch (Exception err)
            {
                var msg = $"Error accessing PrivateKey of the X509 Certificate. Cert: {cert.Thumbprint}";
                throw new Exception(msg, err);
            }
        }

        #endregion

    }
}
