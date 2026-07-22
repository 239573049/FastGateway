namespace Certes.Properties
{
    /// <summary>
    /// Localized (English) message strings.
    /// Replaces the resx-generated resource class to keep the assembly free of
    /// <see cref="System.Resources.ResourceManager"/> reflection (AOT/trim friendly).
    /// </summary>
    internal static class Strings
    {
        public const string ErrorFetchNonce = "Fail to fetch new nonce.";
        public const string ErrorFetchResource = "Fail to load resource from '{0}'.";
        public const string ErrorFinalizeFailed = "Fail to finalize order.";
        public const string ErrorHttpRequest = "Fail to invoke '{0}'.";
        public const string ErrorInvalidBase64String = "Illegal base64url string.";
        public const string ErrorInvalidKeyData = "Invaid key data.";
        public const string ErrorInvalidOrderStatusForFinalize = "Can not finalize order with status '{0}'.";
        public const string ErrorIssuerNotFound = "Can not find issuer '{0}' for certificate '{1}'.";
        public const string ErrorMissingCertificateData = "Certificate data missing, please fetch the certificate from '{0}'.";
        public const string ErrorUnsupportedResourceType = "Unsupported resource type '{0}'.";
    }
}
