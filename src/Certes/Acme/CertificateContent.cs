using System.Security.Cryptography.X509Certificates;

namespace Certes.Acme
{
    internal class CertificateContent : IEncodable
    {
        private readonly string pem;

        public CertificateContent(string pem)
        {
            this.pem = pem.Trim();
        }

        public byte[] ToDer()
        {
            using (var cert = X509Certificate2.CreateFromPem(pem))
            {
                return cert.RawData;
            }
        }

        public string ToPem() => pem;
    }
}
