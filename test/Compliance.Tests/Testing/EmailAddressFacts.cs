using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds facts on an email address aggregate with the operation provenance native facts require.</summary>
static class EmailAddressFacts
{
    static readonly ActorReference Actor = new("anonymous", "anonymous", "Anonymous recovery request");

    public static ImmutableContentRef Content { get; } = new(Uuid.CreateVersion4(), new string('1', 64), 2,
        "application/json", "test-content.v1");

    public static OperationProvenance Operation(EmailAddress address, Uuid logicalId, string commandKind) =>
        Operation(address.Stream, logicalId, commandKind);

    public static OperationProvenance Operation(EventStreamAddress owner, Uuid logicalId, string commandKind) =>
        new(logicalId, logicalId, null, commandKind, Actor, owner, logicalId.ToString(), null,
            new string('a', 64), new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), owner, owner);

    /// <summary>Reserves the address as the native acceptance of a fresh reservation command.</summary>
    public static Result Reserve(this EmailAddress address, Uuid userId, long expectedRevision = -1) =>
        address.Reserve(Operation(address, Uuid.CreateVersion4(), "ReserveEmail"), Content, userId, expectedRevision);

    /// <summary>Issues a verification challenge as the native acceptance of an issue command with the challenge as logical identity.</summary>
    public static Result IssueChallenge(this EmailAddress address, Uuid userId, Uuid challengeId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, string tokenKeyId = "test") =>
        address.IssueChallenge(Operation(address, challengeId, "IssueEmailChallenge"), Content, userId, challengeId,
            tokenHash, expiresAt, now, tokenKeyId);

    /// <summary>Verifies the current challenge as the native acceptance of a verification with the challenge as logical identity.</summary>
    public static Result CompleteChallenge(this EmailAddress address, Uuid userId, string token, DateTimeOffset now)
    {
        var challengeId = address.CurrentChallengeId ?? Uuid.Empty;
        return address.CompleteChallenge(Operation(address, challengeId, "CompleteEmailChallenge"), Content, userId,
            challengeId, token, now);
    }

    /// <summary>The reserved fact of an address as a projector would receive it.</summary>
    public static EmailAddressReserved ReservedFact(Uuid userId, string email) =>
        new(userId, email, Operation(new EmailAddress(email), Uuid.CreateVersion4(), "ReserveEmail"), Content, null);

    /// <summary>The verified fact of an address as a projector would receive it.</summary>
    public static EmailAddressVerified VerifiedFact(Uuid userId, string email, Uuid challengeId) =>
        new(userId, email, challengeId, Operation(new EmailAddress(email), challengeId, "CompleteEmailChallenge"),
            Content, null);

    /// <summary>The issued fact of a verification challenge as a reactor would receive it.</summary>
    public static EmailChallengeIssued VerificationIssuedFact(Uuid userId, string email, Uuid challengeId,
        string tokenHash, DateTimeOffset expiresAt, string tokenKeyId) =>
        new(userId, email, challengeId, tokenHash, expiresAt, tokenKeyId,
            Operation(new EmailAddress(email), challengeId, "IssueEmailChallenge"), Content, null);

    /// <summary>Issues a recovery challenge as the native acceptance of a start with the challenge as logical identity.</summary>
    public static Result IssueRecovery(this EmailAddress address, Uuid userId, Uuid challengeId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, string tokenKeyId) =>
        address.IssueRecoveryChallenge(Operation(address, challengeId, "StartIdentityRecovery"), Content, userId,
            challengeId, tokenHash, expiresAt, now, tokenKeyId);

    /// <summary>Claims a recovery challenge as the native acceptance of a completion with the challenge as logical identity.</summary>
    public static Result Claim(this EmailAddress address, Uuid userId, Uuid challengeId, string token,
        Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset now) =>
        address.ClaimIdentityRecovery(Operation(address, challengeId, "CompleteIdentityRecovery"), Content, Content,
            new ActorReference("user", userId.ToString(), replacementIdentityId.ToString()), Content, Content, now, userId,
            challengeId, token, oldIdentityId, replacementIdentityId, now);

    /// <summary>The issued fact of a recovery challenge as a reactor would receive it.</summary>
    public static IdentityRecoveryChallengeIssued IssuedFact(EmailAddress address, Uuid userId, string email,
        Uuid challengeId, string tokenHash, DateTimeOffset expiresAt, string tokenKeyId) =>
        new(userId, email, challengeId, tokenHash, expiresAt, tokenKeyId,
            Operation(address, challengeId, "StartIdentityRecovery"), Content, null);
}
