using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the status of <see cref="Account"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AccountStatus>))]
    public enum AccountStatus
    {
        /// <summary>
        /// The valid status.
        /// </summary>
        [JsonStringEnumMemberName("valid")]
        Valid,

        /// <summary>
        /// The deactivated status, initiated by client.
        /// </summary>
        [JsonStringEnumMemberName("deactivated")]
        Deactivated,

        /// <summary>
        /// The revoked status, initiated by server.
        /// </summary>
        [JsonStringEnumMemberName("revoked")]
        Revoked,
    }
}
