using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using Certes.Jws;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the ACME Account resource.
    /// </summary>
    public class Account
    {
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <remarks>
        /// See <see cref="AccountStatus"/> for possible values.
        /// </remarks>
        /// <value>
        /// The status.
        /// </value>
        [JsonPropertyName("status")]
        public AccountStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets the contact.
        /// </summary>
        /// <value>
        /// The contact.
        /// </value>
        [JsonPropertyName("contact")]
        public IList<string> Contact { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the terms of service is agreed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the terms of service is agreed; otherwise, <c>false</c>.
        /// </value>
        [JsonPropertyName("termsOfServiceAgreed")]
        public bool? TermsOfServiceAgreed { get; set; }


        /// <summary>
        /// Gets or sets an external account binding
        /// </summary>
        /// <value>
        ///  
        /// </value>
        [JsonPropertyName("externalAccountBinding")]
        public ExternalAccountBinding ExternalAccountBinding { get; set; }

        /// <summary>
        /// Gets or sets the orders.
        /// </summary>
        /// <value>
        /// The orders.
        /// </value>
        [JsonPropertyName("orders")]
        public Uri Orders { get; set; }

        /// <summary>
        /// Represents the payload to retrieve existing account by key.
        /// </summary>
        /// <seealso cref="Certes.Acme.Resource.Account" />
        internal class Payload : Account
        {
            /// <summary>
            /// Gets or sets the only return existing flag.
            /// </summary>
            /// <value>
            /// The only return existing flag.
            /// </value>
            // Must be public for the source generator to emit it — see the note on
            // Order.Payload.Csr. An internal setter would be silently dropped, breaking
            // the "return existing account" lookup. Payload is internal, so the public
            // accessor does not widen the API surface.
            [JsonPropertyName("onlyReturnExisting")]
            [JsonInclude]
            public bool? OnlyReturnExisting { get; set; }
        }
    }
}
