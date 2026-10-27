using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>The native acceptance of <see cref="CompleteEmailChallenge" />; the bearer token never enters the fact.</summary>
[Discriminator("bdgrz.email-address.verified", 1)]
public sealed record EmailAddressVerified(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
