using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>Immutable registration provenance for presentation admitted by active identity authority.</summary>
public sealed record IdentityRegistrationProof(Uuid IdentityId, Uuid EventId, ulong Revision,
    ulong Position, string Sha256);
