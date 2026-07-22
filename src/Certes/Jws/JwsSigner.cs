using System;
using System.Text;
using System.Text.Json;
using Certes.Json;

namespace Certes.Jws
{
    /// <summary>
    /// Represents an signer for JSON Web Signature.
    /// </summary>
    internal class JwsSigner
    {
        private readonly IKey keyPair;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwsSigner"/> class.
        /// </summary>
        /// <param name="keyPair">The keyPair.</param>
        public JwsSigner(IKey keyPair)
        {
            this.keyPair = keyPair;
        }

        /// <summary>
        /// Signs the specified payload.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="nonce">The nonce.</param>
        /// <returns>The signed payload.</returns>
        public JwsPayload Sign(object payload, string nonce)
            => Sign(payload, null, null, nonce);

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="keyId">The key identifier.</param>
        /// <param name="url">The URL.</param>
        /// <param name="nonce">The nonce.</param>
        /// <returns>The signed payload.</returns>
        public JwsPayload Sign(
            object payload,
            Uri keyId = null,
            Uri url = null,
            string nonce = null)
        {
            // Null members are omitted (JsonIgnoreCondition.WhenWritingNull), so the
            // jwk-branch (no kid) and kid-branch (no jwk) match the original output.
            var protectedHeader = keyId == null
                ? new JwsHeader
                {
                    Alg = keyPair.Algorithm.ToJwsAlgorithm(),
                    Jwk = keyPair.JsonWebKey,
                    Nonce = nonce,
                    Url = url,
                }
                : new JwsHeader
                {
                    Alg = keyPair.Algorithm.ToJwsAlgorithm(),
                    Kid = keyId,
                    Nonce = nonce,
                    Url = url,
                };

            var entityJson = payload == null
                ? ""
                : JsonSerializer.Serialize(payload, CertesJsonContext.Default.GetTypeInfo(payload.GetType()));
            var protectedHeaderJson = JsonSerializer.Serialize(protectedHeader, CertesJsonContext.Default.JwsHeader);

            var payloadEncoded = JwsConvert.ToBase64String(Encoding.UTF8.GetBytes(entityJson));
            var protectedHeaderEncoded = JwsConvert.ToBase64String(Encoding.UTF8.GetBytes(protectedHeaderJson));

            var signature = $"{protectedHeaderEncoded}.{payloadEncoded}";
            var signatureBytes = Encoding.UTF8.GetBytes(signature);
            var signedSignatureBytes = keyPair.GetSigner().SignData(signatureBytes);
            var signedSignatureEncoded = JwsConvert.ToBase64String(signedSignatureBytes);

            var body = new JwsPayload
            {
                Protected = protectedHeaderEncoded,
                Payload = payloadEncoded,
                Signature = signedSignatureEncoded
            };

            return body;
        }
    }
}
