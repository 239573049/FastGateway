using System;
using System.Security.Cryptography;

namespace Certes
{
    /// <summary>
    /// The supported algorithms.
    /// </summary>
    public enum KeyAlgorithm
    {
        /// <summary>
        /// RSASSA-PKCS1-v1_5 using SHA-256.
        /// </summary>
        RS256,

        /// <summary>
        /// ECDSA using P-256 and SHA-256.
        /// </summary>
        ES256,

        /// <summary>
        /// ECDSA using P-384 and SHA-384.
        /// </summary>
        ES384,

        /// <summary>
        /// ECDSA using P-521 and SHA-512.
        /// </summary>
        ES512,
    }

    /// <summary>
    /// Helper methods for <see cref="KeyAlgorithm"/>.
    /// </summary>
    public static class KeyAlgorithmExtensions
    {
        /// <summary>
        /// Get the JWS name of the <paramref name="algorithm"/>.
        /// </summary>
        /// <param name="algorithm">The algorithm.</param>
        /// <returns></returns>
        public static string ToJwsAlgorithm(this KeyAlgorithm algorithm)
        {
            if (!Enum.IsDefined(typeof(KeyAlgorithm), algorithm))
            {
                throw new ArgumentException(nameof(algorithm));
            }

            return algorithm.ToString();
        }

        /// <summary>
        /// Gets the .NET <see cref="HashAlgorithmName"/> matching the signature algorithm.
        /// </summary>
        internal static HashAlgorithmName ToHashAlgorithmName(this KeyAlgorithm algo)
        {
            switch (algo)
            {
                case KeyAlgorithm.RS256:
                case KeyAlgorithm.ES256:
                    return HashAlgorithmName.SHA256;
                case KeyAlgorithm.ES384:
                    return HashAlgorithmName.SHA384;
                case KeyAlgorithm.ES512:
                    return HashAlgorithmName.SHA512;
                default:
                    throw new ArgumentException(nameof(algo));
            }
        }

        /// <summary>
        /// Gets the named elliptic curve for the EC signature algorithms.
        /// </summary>
        internal static ECCurve ToEcCurve(this KeyAlgorithm algo)
        {
            switch (algo)
            {
                case KeyAlgorithm.ES256:
                    return ECCurve.NamedCurves.nistP256;
                case KeyAlgorithm.ES384:
                    return ECCurve.NamedCurves.nistP384;
                case KeyAlgorithm.ES512:
                    return ECCurve.NamedCurves.nistP521;
                default:
                    throw new ArgumentException(nameof(algo));
            }
        }
    }
}
