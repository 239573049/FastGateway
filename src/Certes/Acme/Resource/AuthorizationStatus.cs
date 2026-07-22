using System;
using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the status of <see cref="Authorization"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AuthorizationStatus>))]
    public enum AuthorizationStatus
    {
        /// <summary>
        /// The pending status.
        /// </summary>
        [JsonStringEnumMemberName("pending")]
        Pending,

        /// <summary>
        /// The valid status.
        /// </summary>
        [JsonStringEnumMemberName("valid")]
        Valid,

        /// <summary>
        /// The invalid status.
        /// </summary>
        [JsonStringEnumMemberName("invalid")]
        Invalid,

        /// <summary>
        /// The revoked status.
        /// </summary>
        [JsonStringEnumMemberName("revoked")]
        Revoked,

        /// <summary>
        /// The deactivated status.
        /// </summary>
        [JsonStringEnumMemberName("deactivated")]
        Deactivated,

        /// <summary>
        /// The expired status.
        /// </summary>
        [JsonStringEnumMemberName("expired")]
        Expired,
    }
}
