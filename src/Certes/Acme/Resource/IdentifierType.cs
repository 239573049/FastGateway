using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents type of <see cref="Identifier"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<IdentifierType>))]
    public enum IdentifierType
    {
        /// <summary>
        /// The DNS type.
        /// </summary>
        [JsonStringEnumMemberName("dns")]
        Dns,
    }
}
