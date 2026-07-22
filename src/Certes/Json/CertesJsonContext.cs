using System.Text.Json.Serialization;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Jws;

namespace Certes.Json
{
    /// <summary>
    /// Source-generated <see cref="JsonSerializerContext"/> covering every type that
    /// crosses the ACME wire. Enables reflection-free (AOT/trim safe) serialization.
    /// </summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
    // JWS envelope + headers
    [JsonSerializable(typeof(JwsPayload))]
    [JsonSerializable(typeof(JwsHeader))]
    [JsonSerializable(typeof(JsonWebKey))]
    [JsonSerializable(typeof(EcJsonWebKey))]
    [JsonSerializable(typeof(RsaJsonWebKey))]
    [JsonSerializable(typeof(KeyChangePayload))]
    [JsonSerializable(typeof(ExternalAccountBinding))]
    [JsonSerializable(typeof(ExternalAccountBindingHeader))]
    // ACME resources
    [JsonSerializable(typeof(Directory))]
    [JsonSerializable(typeof(DirectoryMeta))]
    [JsonSerializable(typeof(Account))]
    [JsonSerializable(typeof(Account.Payload), TypeInfoPropertyName = "AccountPayload")]
    [JsonSerializable(typeof(Order))]
    [JsonSerializable(typeof(Order.Payload), TypeInfoPropertyName = "OrderPayload")]
    [JsonSerializable(typeof(OrderList))]
    [JsonSerializable(typeof(Authorization))]
    [JsonSerializable(typeof(Challenge))]
    [JsonSerializable(typeof(Identifier))]
    [JsonSerializable(typeof(AcmeError))]
    [JsonSerializable(typeof(CertificateRevocation))]
    // enums
    [JsonSerializable(typeof(AccountStatus))]
    [JsonSerializable(typeof(OrderStatus))]
    [JsonSerializable(typeof(AuthorizationStatus))]
    [JsonSerializable(typeof(ChallengeStatus))]
    [JsonSerializable(typeof(IdentifierType))]
    [JsonSerializable(typeof(RevocationReason))]
    // primitives that appear as top-level T in AcmeHttpClient
    [JsonSerializable(typeof(string))]
    internal partial class CertesJsonContext : JsonSerializerContext
    {
    }
}
