using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Certes.Crypto;

namespace Certes.Pkcs
{
    /// <summary>
    /// Represents a CSR builder.
    /// </summary>
    /// <seealso cref="Certes.Pkcs.ICertificationRequestBuilder" />
    public class CertificationRequestBuilder : ICertificationRequestBuilder
    {
        // RDN keys in the conventional order they appear in a distinguished name.
        private static readonly string[] NameOrder = { "CN", "OU", "O", "L", "ST", "C" };

        private readonly List<(string Key, string Value)> attributes = new List<(string, string)>();
        private IList<string> subjectAlternativeNames = new List<string>();

        /// <summary>
        /// Gets the key.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public IKey Key { get; }

        /// <summary>
        /// Gets the subject alternative names.
        /// </summary>
        /// <value>
        /// The subject alternative names.
        /// </value>
        public IList<string> SubjectAlternativeNames
        {
            get => subjectAlternativeNames;
            set => subjectAlternativeNames = value ?? throw new ArgumentNullException(nameof(SubjectAlternativeNames));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationRequestBuilder"/> class.
        /// </summary>
        public CertificationRequestBuilder()
            : this(KeyFactory.NewKey(KeyAlgorithm.RS256))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificationRequestBuilder"/> class.
        /// </summary>
        /// <param name="key">The key.</param>
        public CertificationRequestBuilder(IKey key)
        {
            Key = key;
        }

        /// <summary>
        /// Adds the distinguished name as certificate subject.
        /// </summary>
        /// <param name="distinguishedName">The distinguished name.</param>
        public void AddName(string distinguishedName)
        {
            // Validate via the framework parser, then record each RDN individually
            // so we can render them in a deterministic order later.
            X500DistinguishedName parsed;
            try
            {
                parsed = new X500DistinguishedName(distinguishedName);
            }
            catch (CryptographicException ex)
            {
                throw new ArgumentOutOfRangeException(
                    $"{distinguishedName} contains an ivalid X509 name.", ex);
            }

            foreach (var rdn in parsed.Format(true)
                         .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = rdn.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = rdn.Substring(0, idx).Trim();
                var value = rdn.Substring(idx + 1).Trim();
                attributes.Add((key, value));
            }
        }

        /// <summary>
        /// Adds the name.
        /// </summary>
        /// <param name="keyOrCommonName">Name of the key or common.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// If <paramref name="keyOrCommonName"/> is not a valid X509 name.
        /// </exception>
        public void AddName(string keyOrCommonName, string value)
            => AddName($"{keyOrCommonName}={value}");

        /// <summary>
        /// Generates the CSR.
        /// </summary>
        /// <returns>
        /// The CSR data.
        /// </returns>
        public byte[] Generate()
        {
            var request = CreateRequest();
            return request.CreateSigningRequest();
        }

        private CertificateRequest CreateRequest()
        {
            var subject = BuildDistinguishedName();

            var cipherKey = (AsymmetricCipherKey)Key;
            var hashName = Key.Algorithm.ToHashAlgorithmName();
            var request = cipherKey.IsRsa
                ? new CertificateRequest(subject, cipherKey.Rsa, hashName, RSASignaturePadding.Pkcs1)
                : new CertificateRequest(subject, cipherKey.ECDsa, hashName);

            if (SubjectAlternativeNames.Count == 0)
            {
                var commonName = attributes.FirstOrDefault(a => a.Key == "CN").Value;
                if (!string.IsNullOrEmpty(commonName))
                {
                    SubjectAlternativeNames.Add(commonName);
                }
            }

            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var name in SubjectAlternativeNames.Distinct())
            {
                sanBuilder.AddDnsName(name);
            }

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.NonRepudiation,
                    false));
            request.CertificateExtensions.Add(sanBuilder.Build());

            return request;
        }

        private X500DistinguishedName BuildDistinguishedName()
        {
            // Render RDNs in a stable, conventional order (CN last-to-first per RFC 4514
            // reversal handled by X500DistinguishedName). Deduplicate keeping first value.
            var ordered = attributes
                .GroupBy(a => a.Key)
                .Select(g => (Key: g.Key, Value: g.First().Value))
                .OrderBy(a =>
                {
                    var i = Array.IndexOf(NameOrder, a.Key);
                    return i < 0 ? int.MaxValue : i;
                })
                .ToList();

            var sb = new StringBuilder();
            foreach (var (key, value) in ordered)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(key).Append('=').Append(Escape(value));
            }

            return new X500DistinguishedName(sb.ToString());
        }

        private static string Escape(string value)
        {
            // Escape the RFC 4514 special characters so the DN string parses back cleanly.
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c == ',' || c == '+' || c == '"' || c == '\\' ||
                    c == '<' || c == '>' || c == ';' || c == '=')
                {
                    sb.Append('\\');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
