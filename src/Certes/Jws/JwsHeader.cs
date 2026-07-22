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

        // Declared as object (not JsonWebKey) on purpose: System.Text.Json's source
        // generator serializes a member by its *declared* type, so a JsonWebKey-typed
        // property would emit only the base "kty" and drop the derived key material
        // (crv/x/y or n/e), producing a malformed jwk that Let's Encrypt rejects with
        // "Parse error reading JWS". object triggers runtime-type serialization, and
        // EcJsonWebKey/RsaJsonWebKey are both registered in CertesJsonContext.
        [JsonPropertyName("jwk")]
        public object Jwk { get; set; }

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

        // object (not JsonWebKey) so the derived key material serializes — see the note
        // on JwsHeader.Jwk above. Otherwise account key rollover sends a malformed oldKey.
        [JsonPropertyName("oldKey")]
        public object OldKey { get; set; }
    }

    /// <summary>
    /// An empty JWS payload that serializes to <c>{}</c>. Used for requests whose body
    /// is an empty JSON object (e.g. the RFC 8555 §7.5.1 challenge-validation trigger).
    /// A concrete, source-gen-registered type is required so serialization is AOT-safe;
    /// an anonymous <c>new {}</c> has no JsonTypeInfo and throws under Native AOT.
    /// </summary>
    internal class EmptyObject
    {
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
