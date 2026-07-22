using System.Text.Json;
using System.Text.Json.Serialization;

namespace Certes.Json
{
    /// <summary>
    /// Helper methods for JSON serialization.
    /// </summary>
    public static class JsonUtil
    {
        private static readonly JsonSerializerOptions settings = CreateSettings();

        /// <summary>
        /// Creates the <see cref="JsonSerializerOptions"/> used for ACME entity serialization.
        /// </summary>
        /// <returns>The JSON serializer options.</returns>
        public static JsonSerializerOptions CreateSettings()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
                TypeInfoResolver = CertesJsonContext.Default,
            };

            return options;
        }

        /// <summary>
        /// Gets the shared, source-generated <see cref="JsonSerializerOptions"/> instance.
        /// </summary>
        internal static JsonSerializerOptions Settings => settings;
    }
}
