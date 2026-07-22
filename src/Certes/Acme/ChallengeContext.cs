using System;
using System.Threading.Tasks;
using Certes.Acme.Resource;
using Certes.Jws;

namespace Certes.Acme
{
    /// <summary>
    /// Represents the context for ACME challenge operations.
    /// </summary>
    /// <seealso cref="Certes.Acme.IChallengeContext" />
    internal class ChallengeContext : EntityContext<Challenge>, IChallengeContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChallengeContext"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="location">The location.</param>
        /// <param name="type">The type.</param>
        /// <param name="token">The token.</param>
        public ChallengeContext(
            IAcmeContext context,
            Uri location,
            string type,
            string token)
            : base(context, location)
        {
            Type = type;
            Token = token;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public string Type { get; }

        /// <summary>
        /// Gets the token.
        /// </summary>
        /// <value>
        /// The token.
        /// </value>
        public string Token { get; }

        /// <summary>
        /// Gets the key authorization string.
        /// </summary>
        /// <value>
        /// The key authorization string.
        /// </value>
        public string KeyAuthz => Context.AccountKey.KeyAuthorization(Token);

        /// <summary>
        /// Acknowledges the ACME server the challenge is ready for validation.
        /// </summary>
        /// <returns>
        /// The challenge.
        /// </returns>
        public async Task<Challenge> Validate()
        {
            // RFC 8555 §7.5.1: the challenge-trigger POST carries an empty JSON object.
            // Must be a concrete, source-gen-registered type (EmptyObject) — an anonymous
            // `new {}` has no JsonTypeInfo under Native AOT and throws in JwsSigner.Sign.
            var resp = await Context.HttpClient.Post<Challenge>(Context, Location, new EmptyObject(), true);
            return resp.Resource;
        }
    }
}
