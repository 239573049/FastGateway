using System;
using System.Text.Json.Serialization;

namespace Certes.Jws
{
    /// <summary>
    /// The JWS protected header. Replaces the anonymous objects previously used so
    /// serialization is reflection-free (AOT/trim safe). Null members are omitted.
    /// </summary>
    internal class JwsHeader
    {
        [JsonPropertyName("alg")]
        public string Alg { get; set; }

        [JsonPropertyName("jwk")]
        public JsonWebKey Jwk { get; set; }

        [JsonPropertyName("kid")]
        public Uri Kid { get; set; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }
    }

    /// <summary>
    /// The payload used when changing the account key.
    /// </summary>
    internal class KeyChangePayload
    {
        [JsonPropertyName("account")]
        public Uri Account { get; set; }

        [JsonPropertyName("oldKey")]
        public JsonWebKey OldKey { get; set; }
    }

    /// <summary>
    /// External account binding JWS, embedded into the new-account request.
    /// </summary>
    public class ExternalAccountBinding
    {
        /// <summary>
        /// The base64url-encoded protected header.
        /// </summary>
        [JsonPropertyName("protected")]
        public string Protected { get; set; }

        /// <summary>
        /// The base64url-encoded payload (the account JWK).
        /// </summary>
        [JsonPropertyName("payload")]
        public string Payload { get; set; }

        /// <summary>
        /// The base64url-encoded HMAC signature.
        /// </summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; }
    }

    /// <summary>
    /// The EAB protected header (alg / kid / url).
    /// </summary>
    internal class ExternalAccountBindingHeader
    {
        [JsonPropertyName("alg")]
        public string Alg { get; set; }

        [JsonPropertyName("kid")]
        public string Kid { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }
    }
}
