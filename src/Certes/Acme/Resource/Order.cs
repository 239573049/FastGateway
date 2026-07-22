using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the ACME Order resource.
    /// </summary>
    public class Order
    {
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        /// <remarks>
        /// See <see cref="OrderStatus"/> for possible values.
        /// </remarks>
        [JsonPropertyName("status")]
        public OrderStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets the expires.
        /// </summary>
        /// <value>
        /// The expires.
        /// </value>
        [JsonPropertyName("expires")]
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// Gets or sets the identifiers.
        /// </summary>
        /// <value>
        /// The identifiers.
        /// </value>
        public IList<Identifier> Identifiers { get; set; }

        /// <summary>
        /// Gets or sets the not before.
        /// </summary>
        /// <value>
        /// The not before.
        /// </value>
        [JsonPropertyName("notBefore")]
        public DateTimeOffset? NotBefore { get; set; }

        /// <summary>
        /// Gets or sets the not after.
        /// </summary>
        /// <value>
        /// The not after.
        /// </value>
        [JsonPropertyName("notAfter")]
        public DateTimeOffset? NotAfter { get; set; }

        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        /// <remarks>
        /// TODO: model https://tools.ietf.org/html/rfc7807
        /// </remarks>
        [JsonPropertyName("error")]
        public object Error { get; set; }

        /// <summary>
        /// Gets or sets the authorizations.
        /// </summary>
        /// <value>
        /// The authorizations.
        /// </value>
        [JsonPropertyName("authorizations")]
        public IList<Uri> Authorizations { get; set; }

        /// <summary>
        /// Gets or sets the finalize.
        /// </summary>
        /// <value>
        /// The finalize.
        /// </value>
        [JsonPropertyName("finalize")]
        public Uri Finalize { get; set; }

        /// <summary>
        /// Gets or sets the certificate.
        /// </summary>
        /// <value>
        /// The certificate.
        /// </value>
        [JsonPropertyName("certificate")]
        public Uri Certificate { get; set; }

        /// <summary>
        /// Represents the payload to finalize an order.
        /// </summary>
        /// <seealso cref="Certes.Acme.Resource.Order" />
        internal class Payload : Order
        {
            /// <summary>
            /// Gets or sets the CSR.
            /// </summary>
            /// <value>
            /// The CSR.
            /// </value>
            // Must be public: System.Text.Json's source generator serializes only public
            // members, so an internal Csr is silently dropped and finalize sends a payload
            // with no csr — the server then fails with "asn1: syntax error: sequence
            // truncated". The enclosing Payload class is internal, so this does not widen
            // the public API surface.
            [JsonPropertyName("csr")]
            [JsonInclude]
            public string Csr { get; set; }
        }
    }
}
