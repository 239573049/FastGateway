using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Certes.Crypto;
using Certes.Json;
using Certes.Jws;

namespace Certes
{
    /// <summary>
    /// Represents key parameters used for signing.
    /// </summary>
    public interface IKey : IEncodable
    {
        /// <summary>
        /// Gets the algorithm.
        /// </summary>
        /// <value>
        /// The algorithm.
        /// </value>
        KeyAlgorithm Algorithm { get; }

        /// <summary>
        /// Gets the json web key.
        /// </summary>
        /// <value>
        /// The json web key.
        /// </value>
        JsonWebKey JsonWebKey { get; }
    }

    /// <summary>
    /// Helper methods for <see cref="AccountKey"/>.
    /// </summary>
    public static class ISignatureKeyExtensions
    {
        // ACME tls-alpn-01 extension OID (id-pe-acmeIdentifier).
        private const string AcmeValidationV1Id = "1.3.6.1.5.5.7.1.31";

        /// <summary>
        /// Generates the thumbprint for the given account <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The account key.</param>
        /// <returns>The thumbprint.</returns>
        internal static byte[] GenerateThumbprint(this IKey key)
        {
            var jwk = key.JsonWebKey;
            var json = JsonSerializer.Serialize(jwk, CertesJsonContext.Default.GetTypeInfo(jwk.GetType()));
            var bytes = Encoding.UTF8.GetBytes(json);
            return SHA256.HashData(bytes);
        }

        /// <summary>
        /// Generates the base64 encoded thumbprint for the given account <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The account key.</param>
        /// <returns>The thumbprint.</returns>
        public static string Thumbprint(this IKey key)
        {
            var jwkThumbprint = key.GenerateThumbprint();
            return JwsConvert.ToBase64String(jwkThumbprint);
        }

        /// <summary>
        /// Generates key authorization string.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="token">The challenge token.</param>
        /// <returns>The key authorization string.</returns>
        public static string KeyAuthorization(this IKey key, string token)
        {
            var jwkThumbprintEncoded = key.Thumbprint();
            return $"{token}.{jwkThumbprintEncoded}";
        }

        /// <summary>
        /// Generates the value for DNS TXT record.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="token">The challenge token.</param>
        /// <returns>The DNS text value for dns-01 validation.</returns>
        public static string DnsTxt(this IKey key, string token)
        {
            var keyAuthz = key.KeyAuthorization(token);
            var hashed = SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthz));
            return JwsConvert.ToBase64String(hashed);
        }

        /// <summary>
        /// Generates the certificate for <see cref="Acme.Resource.ChallengeTypes.TlsAlpn01" /> validation.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="token">The tls-alpn-01 token.</param>
        /// <param name="subjectName">Name of the subject.</param>
        /// <param name="certificateKey">The certificate key pair.</param>
        /// <returns>The tls-alpn-01 certificate in PEM.</returns>
        public static string TlsAlpnCertificate(this IKey key, string token, string subjectName, IKey certificateKey)
        {
            var keyAuthz = key.KeyAuthorization(token);
            var hashed = SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthz));

            var cipherKey = (AsymmetricCipherKey)certificateKey;
            var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

            CertificateRequest request;
            if (cipherKey.IsRsa)
            {
                request = new CertificateRequest(
                    distinguishedName, cipherKey.Rsa,
                    certificateKey.Algorithm.ToHashAlgorithmName(), RSASignaturePadding.Pkcs1);
            }
            else
            {
                request = new CertificateRequest(
                    distinguishedName, cipherKey.ECDsa, certificateKey.Algorithm.ToHashAlgorithmName());
            }

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(subjectName);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // ACME-TLS/1 critical extension carrying the key-authorization digest,
            // wrapped in a DER OCTET STRING as required by RFC 8737.
            var extValue = new byte[hashed.Length + 2];
            extValue[0] = 0x04; // OCTET STRING tag
            extValue[1] = (byte)hashed.Length;
            Array.Copy(hashed, 0, extValue, 2, hashed.Length);
            request.CertificateExtensions.Add(
                new X509Extension(new Oid(AcmeValidationV1Id), extValue, critical: true));

            var now = DateTimeOffset.UtcNow;
            using (var cert = request.CreateSelfSigned(now, now.AddDays(7)))
            {
                return new string(PemEncoding.Write("CERTIFICATE", cert.RawData));
            }
        }
    }
}
