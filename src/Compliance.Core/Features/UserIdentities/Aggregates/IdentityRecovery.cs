using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Aggregates;

/// <summary>
///     The owner of one identity recovery challenge. It materializes the immutable both-proof-verified candidate,
///     accepts the swap only after both identity slots are protected, and completes it forward; before acceptance
///     a definite conflict rejects the preparation and fences every delayed receiver. Stream identity is the
///     challenge, so a later challenge can never rewrite an earlier recovery.
/// </summary>
public sealed class IdentityRecovery : Aggregate
{
    /// <summary>The one generation a challenge freezes.</summary>
    public const long FrozenGeneration = 1;

    IdentityRecoveryPrepared? _prepared;

    /// <summary>Hydrates the recovery of one challenge for the receiver command selected by <paramref name="selectedCommandId" />.</summary>
    public IdentityRecovery(Uuid challengeId, Uuid selectedCommandId = default)
        : base(challengeId, new EventStreamAddress("bdgrz", "identity-recoveries", challengeId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<IdentityRecoveryPrepared>(fact =>
        {
            Acceptance.Observe(fact);
            Require(fact, Stage.None);
            _prepared = fact;
            State = Stage.Prepared;
        });
        On<IdentityRecoveryAccepted>(fact =>
        {
            Acceptance.Observe(fact);
            Require(fact, Stage.Prepared);
            State = Stage.Accepted;
        });
        On<IdentityRecoveryCompleted>(fact =>
        {
            Acceptance.Observe(fact);
            Require(fact, Stage.Accepted);
            State = Stage.Completed;
        });
        On<IdentityRecoveryPreparationRejected>(fact =>
        {
            Acceptance.Observe(fact);
            Require(fact, Stage.Prepared);
            State = Stage.Rejected;
        });
    }

    /// <summary>Where the recovery stands.</summary>
    public enum Stage
    {
        /// <summary>Nothing was materialized yet.</summary>
        None,

        /// <summary>The candidate is frozen; the swap is not accepted.</summary>
        Prepared,

        /// <summary>The swap is accepted and completes forward.</summary>
        Accepted,

        /// <summary>The swap finished.</summary>
        Completed,

        /// <summary>The preparation was rejected before acceptance.</summary>
        Rejected,
    }

    /// <summary>Acceptance of the receiver command this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The current stage.</summary>
    public Stage State { get; private set; }

    /// <summary>The frozen intent of the prepared candidate; null until it is prepared.</summary>
    public ImmutableContentRef? Intent => _prepared?.Intent;

    /// <summary>Freezes the candidate. A challenge freezes exactly one candidate.</summary>
    public Result Materialize(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? sourceProofs)
    {
        if (State != Stage.None)
            return Failure("The recovery candidate was already frozen.");
        Raise(new IdentityRecoveryPrepared(Stream, (long)Version, FrozenGeneration, intent, sourceProofs, operation,
            Challenge, null, null));
        return Result.Success;
    }

    /// <summary>Accepts the swap once both identity slots are protected. A rejected preparation can never be accepted.</summary>
    public Result Accept(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? protectionProofs)
    {
        if (State != Stage.Prepared || intent != _prepared!.Intent)
            return Failure("Only the frozen, unrejected preparation can be accepted.");
        Raise(new IdentityRecoveryAccepted(Stream, (long)Version, FrozenGeneration, intent, protectionProofs, operation,
            Challenge, null, null));
        return Result.Success;
    }

    /// <summary>Completes the accepted swap.</summary>
    public Result Complete(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? completionProofs)
    {
        if (State != Stage.Accepted || intent != _prepared!.Intent)
            return Failure("Only the accepted swap can be completed.");
        Raise(new IdentityRecoveryCompleted(Stream, (long)Version, FrozenGeneration, intent, completionProofs, operation,
            Challenge, null, null));
        return Result.Success;
    }

    /// <summary>Rejects the preparation before acceptance; an accepted swap recovers forward and cannot be rejected.</summary>
    public Result Reject(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? conflictProof)
    {
        if (State != Stage.Prepared || intent != _prepared!.Intent)
            return Failure("Only a prepared, not yet accepted recovery can be rejected.");
        Raise(new IdentityRecoveryPreparationRejected(Stream, (long)Version, FrozenGeneration, intent, conflictProof,
            operation, Challenge, null, null));
        return Result.Success;
    }

    Uuid Challenge => Id;

    void Raise(DomainEvent fact) => RaiseEvent(fact);

    void Require(IIdentityRecoveryFact fact, Stage expected)
    {
        if (State != expected || fact.Owner != Stream || fact.ChallengeId != Id ||
            fact.Generation != FrozenGeneration || _prepared is not null && fact.Intent != _prepared.Intent)
            throw new InvalidOperationException(
                $"{fact.GetType().Name} does not follow the {State} identity recovery of its challenge.");
    }

    static Result Failure(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, false));
}
