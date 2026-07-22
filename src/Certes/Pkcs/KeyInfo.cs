using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace Certes.Pkcs
{
    /// <summary>
    /// Represents a key pair.
    /// </summary>
    public class KeyInfo
    {
        /// <summary>
        /// Gets or sets the private key information (PKCS#8 DER encoded).
        /// </summary>
        /// <value>
        /// The private key information.
        /// </value>
        [JsonPropertyName("der")]
        public byte[] PrivateKeyInfo { get; set; }

        /// <summary>
        /// Reads the key from the given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The steam.</param>
        /// <returns>The key loaded.</returns>
        public static KeyInfo From(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                var pem = streamReader.ReadToEnd();
                var key = KeyFactory.FromPem(pem);
                return new KeyInfo { PrivateKeyInfo = key.ToDer() };
            }
        }
    }

    /// <summary>
    /// Helper methods for <see cref="KeyInfo"/>.
    /// </summary>
    public static class KeyInfoExtensions
    {
        /// <summary>
        /// Saves the key pair to the specified stream.
        /// </summary>
        /// <param name="keyInfo">The key information.</param>
        /// <param name="stream">The stream.</param>
        public static void Save(this KeyInfo keyInfo, Stream stream)
        {
            var key = KeyFactory.FromDer(keyInfo.PrivateKeyInfo);
            var pem = key.ToPem();
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(pem);
            }
        }
    }
}
