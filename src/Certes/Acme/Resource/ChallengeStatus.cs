using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the status for <see cref="Challenge"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ChallengeStatus>))]
    public enum ChallengeStatus
    {
        /// <summary>
        /// The pending status.
        /// </summary>
        [JsonStringEnumMemberName("pending")]
        Pending,

        /// <summary>
        /// The processing status.
        /// </summary>
        [JsonStringEnumMemberName("processing")]
        Processing,

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
    }
}
