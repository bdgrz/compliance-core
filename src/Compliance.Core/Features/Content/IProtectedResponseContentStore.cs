using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>Persistent, scoped, authenticated encryption of confidential original responses.</summary>
public interface IProtectedResponseContentStore
{
    /// <summary>Fails closed when no configured active persistent encryption key is available.</summary>
    void RequireConfigured();

    /// <summary>Publishes a fresh encrypted candidate and verifies it before returning its opaque reference.</summary>
    Task<ProtectedResponseContentRef> WriteVerifiedAsync(ProtectedResponseScope scope, string schema,
        ReadOnlyMemory<byte> plaintext, CancellationToken ct = default);

    /// <summary>Authenticates the exact scope, schema, envelope and configured key before returning plaintext.</summary>
    Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(ProtectedResponseScope scope, ProtectedResponseContentRef reference,
        CancellationToken ct = default);
}
