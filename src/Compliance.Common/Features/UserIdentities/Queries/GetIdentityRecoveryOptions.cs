using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Queries;

[Discriminator("bdgrz.user-identity.recovery-options", 1)]
public sealed record GetIdentityRecoveryOptions(
    string EmailAddress,
    Uuid ChallengeId,
    string Token,
    int? Limit = null,
    string? Cursor = null,
    long? MinimumProjectionRevision = null) : IRequest<IdentityRecoveryOptions>, ICallable;

public sealed record IdentityRecoveryOptions(
    long ProjectionRevision,
    Page<IdentityRecoveryOption> Identities);

public sealed record IdentityRecoveryOption(Uuid UserIdentityId, string Provider);
