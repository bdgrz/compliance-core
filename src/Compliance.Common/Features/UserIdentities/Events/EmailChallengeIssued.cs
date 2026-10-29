using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>
///     The native acceptance of <see cref="IssueEmailChallenge" />. It carries the hash of the bearer token, never the
///     token; delivery of the token to the mailbox is a technical effect of the email challenge delivery process and
///     leaves no fact on the address stream.
/// </summary>
[Discriminator("bdgrz.email-address.challenge-issued", 1)]
public sealed record EmailChallengeIssued(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    string TokenKeyId,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
