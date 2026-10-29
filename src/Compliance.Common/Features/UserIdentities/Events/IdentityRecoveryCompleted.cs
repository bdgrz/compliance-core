using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Events;

/// <summary>The accepted swap finished: the replacement is registered and the old identity revoked to it.</summary>
[Discriminator("bdgrz.identity-recovery.identity-recovery-completed", 1)]
public sealed record IdentityRecoveryCompleted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation, Uuid ChallengeId,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IIdentityRecoveryFact;
