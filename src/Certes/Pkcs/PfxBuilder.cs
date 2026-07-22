using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes.Crypto;

namespace Certes.Pkcs
{
    /// <summary>
    /// Supports generating PFX from the certificate and key pair.
    /// </summary>
    public class PfxBuilder
    {
        private readonly byte[] certificateDer;
        private readonly IKey privateKey;
        private readonly List<byte[]> issuers = new List<byte[]>();

        /// <summary>
        /// Gets or sets a value indicating whether to include the full certificate chain in the PFX.
        /// </summary>
        /// <value>
        ///   <c>true</c> if include the full certificate chain in the PFX; otherwise, <c>false</c>.
        /// </value>
        public bool FullChain { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="PfxBuilder"/> class.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <param name="privateKeyInfo">The private key information.</param>
        public PfxBuilder(byte[] certificate, KeyInfo privateKeyInfo)
            : this(certificate, KeyFactory.FromDer(privateKeyInfo.PrivateKeyInfo))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PfxBuilder"/> class.
        /// </summary>
        /// <param name="certificate">The certificate (DER encoded).</param>
        /// <param name="privateKey">The private key.</param>
        public PfxBuilder(byte[] certificate, IKey privateKey)
        {
            certificateDer = certificate;
            this.privateKey = privateKey;
        }

        /// <summary>
        /// Adds an issuer certificate.
        /// </summary>
        /// <param name="certificate">The issuer certificate (DER encoded).</param>
        public void AddIssuer(byte[] certificate) => issuers.Add(certificate);

        /// <summary>
        /// Adds an issuer certificate (DER encoded).
        /// </summary>
        /// <param name="certificates">The issuer certificate (single DER encoded certificate).</param>
        public void AddIssuers(byte[] certificates)
        {
            using (var cert = X509CertificateLoader.LoadCertificate(certificates))
            {
                issuers.Add(cert.RawData);
            }
        }

        /// <summary>
        /// Builds the PFX with specified friendly name.
        /// </summary>
        /// <param name="friendlyName">The friendly name.</param>
        /// <param name="password">The password.</param>
        /// <returns>The PFX data.</returns>
        public byte[] Build(string friendlyName, string password)
        {
            using (var leaf = X509CertificateLoader.LoadCertificate(certificateDer))
            using (var withKey = BindPrivateKey(leaf))
            {
                var collection = new X509Certificate2Collection { withKey };

                if (FullChain)
                {
                    foreach (var issuerDer in issuers)
                    {
                        collection.Add(X509CertificateLoader.LoadCertificate(issuerDer));
                    }
                }

                return collection.Export(X509ContentType.Pfx, password);
            }
        }

        private X509Certificate2 BindPrivateKey(X509Certificate2 leaf)
        {
            var cipherKey = (AsymmetricCipherKey)privateKey;
            return cipherKey.IsRsa
                ? leaf.CopyWithPrivateKey(cipherKey.Rsa)
                : leaf.CopyWithPrivateKey(cipherKey.ECDsa);
        }
    }
}
