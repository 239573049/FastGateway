using System;
using System.Security.Cryptography;
using Certes.Jws;

namespace Certes.Crypto
{
    /// <summary>
    /// An <see cref="IKey"/> backed by a .NET <see cref="AsymmetricAlgorithm"/>
    /// (<see cref="ECDsa"/> or <see cref="RSA"/>). Also acts as its own signer.
    /// </summary>
    internal sealed class AsymmetricCipherKey : IKey, ISigner
    {
        private readonly RSA rsa;
        private readonly ECDsa ecdsa;

        public KeyAlgorithm Algorithm { get; }

        public AsymmetricCipherKey(KeyAlgorithm algorithm, RSA rsa)
        {
            Algorithm = algorithm;
            this.rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));
        }

        public AsymmetricCipherKey(KeyAlgorithm algorithm, ECDsa ecdsa)
        {
            Algorithm = algorithm;
            this.ecdsa = ecdsa ?? throw new ArgumentNullException(nameof(ecdsa));
        }

        internal bool IsRsa => rsa != null;

        internal RSA Rsa => rsa;

        internal ECDsa ECDsa => ecdsa;

        public JsonWebKey JsonWebKey
        {
            get
            {
                if (Algorithm == KeyAlgorithm.RS256)
                {
                    var p = rsa.ExportParameters(false);
                    return new RsaJsonWebKey
                    {
                        KeyType = "RSA",
                        Exponent = JwsConvert.ToBase64String(p.Exponent),
                        Modulus = JwsConvert.ToBase64String(p.Modulus),
                    };
                }
                else
                {
                    var curve =
                        Algorithm == KeyAlgorithm.ES256 ? "P-256" :
                        Algorithm == KeyAlgorithm.ES384 ? "P-384" : "P-521";

                    // https://tools.ietf.org/html/rfc7518#section-6.2.1.2
                    // ECParameters.Q.X / Q.Y are already fixed-length, left-padded
                    // to the curve field size, so no manual padding is required.
                    var p = ecdsa.ExportParameters(false);
                    return new EcJsonWebKey
                    {
                        KeyType = "EC",
                        Curve = curve,
                        X = JwsConvert.ToBase64String(p.Q.X),
                        Y = JwsConvert.ToBase64String(p.Q.Y),
                    };
                }
            }
        }

        public byte[] ToDer() =>
            rsa != null ? rsa.ExportPkcs8PrivateKey() : ecdsa.ExportPkcs8PrivateKey();

        public string ToPem() =>
            rsa != null ? rsa.ExportPkcs8PrivateKeyPem() : ecdsa.ExportPkcs8PrivateKeyPem();

        public byte[] ComputeHash(byte[] data)
        {
            using (var hasher = IncrementalHash.CreateHash(Algorithm.ToHashAlgorithmName()))
            {
                hasher.AppendData(data);
                return hasher.GetHashAndReset();
            }
        }

        public byte[] SignData(byte[] data)
        {
            var hashName = Algorithm.ToHashAlgorithmName();
            if (rsa != null)
            {
                return rsa.SignData(data, hashName, RSASignaturePadding.Pkcs1);
            }

            // .NET's ECDsa.SignData defaults to the IEEE P1363 (r || s) fixed-length
            // format required by JWS — no DER-to-P1363 conversion needed.
            return ecdsa.SignData(data, hashName);
        }
    }
}
