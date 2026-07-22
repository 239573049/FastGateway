using System.IO;
using Certes.Acme;
using Certes.Pkcs;

namespace Certes
{
    /// <summary>
    /// Extension methods for <see cref="CertificateChain"/>.
    /// </summary>
    public static class CertificateChainExtensions
    {
        /// <summary>
        /// Converts the certificate to PFX with the key.
        /// </summary>
        /// <param name="certificateChain">The certificate chain.</param>
        /// <param name="certKey">The certificate private key.</param>
        /// <returns>The PFX.</returns>
        public static PfxBuilder ToPfx(this CertificateChain certificateChain, IKey certKey)
        {
            var pfx = new PfxBuilder(certificateChain.Certificate.ToDer(), certKey);
            if (certificateChain.Issuers != null)
            {
                foreach (var issuer in certificateChain.Issuers)
                {
                    pfx.AddIssuer(issuer.ToDer());
                }
            }

            return pfx;
        }

        /// <summary>
        /// Encodes the full certificate chain in PEM.
        /// </summary>
        /// <param name="certificateChain">The certificate chain.</param>
        /// <param name="certKey">The certificate key.</param>
        /// <returns>The encoded certificate chain.</returns>
        public static string ToPem(this CertificateChain certificateChain, IKey certKey = null)
        {
            using (var writer = new StringWriter())
            {
                if (certKey != null)
                {
                    writer.WriteLine(certKey.ToPem().TrimEnd());
                }

                writer.WriteLine(certificateChain.Certificate.ToPem().TrimEnd());

                if (certificateChain.Issuers != null)
                {
                    foreach (var issuer in certificateChain.Issuers)
                    {
                        writer.WriteLine(issuer.ToPem().TrimEnd());
                    }
                }

                return writer.ToString();
            }
        }
    }
}
