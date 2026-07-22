using System;
using System.Security.Cryptography;

namespace Certes.Crypto
{
    /// <summary>
    /// Creates and parses signature keys using <see cref="System.Security.Cryptography"/>.
    /// </summary>
    internal class KeyAlgorithmProvider
    {
        /// <summary>
        /// Generates a new key for the given algorithm.
        /// </summary>
        public IKey GenerateKey(KeyAlgorithm algorithm, int? keySize = null)
        {
            switch (algorithm)
            {
                case KeyAlgorithm.RS256:
                    return new AsymmetricCipherKey(algorithm, RSA.Create(keySize ?? 2048));
                case KeyAlgorithm.ES256:
                case KeyAlgorithm.ES384:
                case KeyAlgorithm.ES512:
                    return new AsymmetricCipherKey(algorithm, ECDsa.Create(algorithm.ToEcCurve()));
                default:
                    throw new ArgumentException(nameof(algorithm));
            }
        }

        /// <summary>
        /// Parses a key from its PKCS#8 (or, for backward compatibility, SEC1 / PKCS#1) DER encoding.
        /// </summary>
        public IKey GetKey(byte[] der)
        {
            // ACME account keys persisted by this library are PKCS#8. Try EC first, then RSA.
            var ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportPkcs8PrivateKey(der, out _);
                return FromEcdsa(ecdsa);
            }
            catch (CryptographicException)
            {
                ecdsa.Dispose();
            }

            var rsa = RSA.Create();
            try
            {
                rsa.ImportPkcs8PrivateKey(der, out _);
                return new AsymmetricCipherKey(KeyAlgorithm.RS256, rsa);
            }
            catch (CryptographicException)
            {
                rsa.Dispose();
                throw new AcmeException(Properties.Strings.ErrorInvalidKeyData);
            }
        }

        /// <summary>
        /// Parses a key from PEM text. Accepts PKCS#8, SEC1 ("EC PRIVATE KEY") and
        /// PKCS#1 ("RSA PRIVATE KEY") to stay backward compatible with keys written
        /// by older (BouncyCastle-based) versions.
        /// </summary>
        public IKey GetKey(string pem)
        {
            if (pem == null)
            {
                throw new AcmeException(Properties.Strings.ErrorInvalidKeyData);
            }

            if (pem.IndexOf("RSA PRIVATE KEY", StringComparison.Ordinal) >= 0)
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                return new AsymmetricCipherKey(KeyAlgorithm.RS256, rsa);
            }

            if (pem.IndexOf("EC PRIVATE KEY", StringComparison.Ordinal) >= 0)
            {
                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);
                return FromEcdsa(ecdsa);
            }

            // Generic PKCS#8 ("PRIVATE KEY"): try EC, fall back to RSA.
            var ec = ECDsa.Create();
            try
            {
                ec.ImportFromPem(pem);
                return FromEcdsa(ec);
            }
            catch (Exception)
            {
                ec.Dispose();
            }

            var rsaKey = RSA.Create();
            try
            {
                rsaKey.ImportFromPem(pem);
                return new AsymmetricCipherKey(KeyAlgorithm.RS256, rsaKey);
            }
            catch (Exception)
            {
                rsaKey.Dispose();
                throw new AcmeException(Properties.Strings.ErrorInvalidKeyData);
            }
        }

        private static IKey FromEcdsa(ECDsa ecdsa)
        {
            var keySize = ecdsa.KeySize;
            KeyAlgorithm algo;
            switch (keySize)
            {
                case 256:
                    algo = KeyAlgorithm.ES256;
                    break;
                case 384:
                    algo = KeyAlgorithm.ES384;
                    break;
                case 521:
                    algo = KeyAlgorithm.ES512;
                    break;
                default:
                    ecdsa.Dispose();
                    throw new NotSupportedException($"Unsupported EC key size {keySize}.");
            }

            return new AsymmetricCipherKey(algo, ecdsa);
        }
    }
}
