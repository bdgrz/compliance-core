using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.NamedRecords.Aggregates;

/// <summary>
///     The authoritative unique active-name reservation of one normalized name. It is held by at most one record
///     stream at a time, tagged with the change generation that last pinned it. A holder may re-pin its own claim for
///     a later generation; a different record is refused; a release only takes effect for the exact holder and
///     generation, so a delayed release can never free a name a later change pinned.
/// </summary>
public abstract class NameClaim : Aggregate
{
    EventStreamAddress? _holder;
    long _heldGeneration;

    /// <summary>Opens the claim owner for the logical operation this hydration was selected for.</summary>
    protected NameClaim(Uuid id, EventStreamAddress stream, Uuid selectedLogicalId) : base(id, stream)
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The record stream holding the name, or null when it is free.</summary>
    public EventStreamAddress? Holder => _holder;

    /// <summary>The change generation that last pinned the claim.</summary>
    public long HeldGeneration => _heldGeneration;

    /// <summary>What a claim by <paramref name="holder" /> at <paramref name="generation" /> would meet.</summary>
    public ClaimVerdict EvaluateClaim(EventStreamAddress holder, long generation)
    {
        ArgumentNullException.ThrowIfNull(holder);
        if (_holder is null)
            return ClaimVerdict.Free;
        if (_holder != holder)
            return ClaimVerdict.Taken;
        return generation >= _heldGeneration ? ClaimVerdict.Pin : ClaimVerdict.Superseded;
    }

    /// <summary>True when the claim is held by exactly this holder and generation.</summary>
    public bool IsHeldBy(EventStreamAddress holder, long generation) =>
        _holder == holder && _heldGeneration == generation;

    /// <summary>
    ///     Claims or re-pins the name as the native fact of <paramref name="operation" />. Each concrete owner raises
    ///     its own typed fact after <see cref="CheckClaim" /> passed.
    /// </summary>
    public abstract Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent);

    /// <summary>Releases the name as the native fact of <paramref name="operation" /> when the exact holder and generation hold it.</summary>
    public abstract Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent);

    /// <summary>Checks a claim: its own logical operation and a name that is free or pinned by the same holder.</summary>
    protected Result CheckClaim(OperationProvenance operation, EventStreamAddress holder, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A name claim requires its own logical operation."));
        return EvaluateClaim(holder, generation) is ClaimVerdict.Free or ClaimVerdict.Pin
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The name is not available.", false));
    }

    /// <summary>Checks a release: its own logical operation and the exact holder and generation.</summary>
    protected Result CheckRelease(OperationProvenance operation, EventStreamAddress holder, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A name release requires its own logical operation."));
        return IsHeldBy(holder, generation)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The name is not held by this change.", false));
    }

    /// <summary>Reducer of the claimed fact.</summary>
    protected void ObserveClaimed(INamedRecordFact fact)
    {
        Acceptance.Observe((DomainEvent)fact);
        if (fact.Operation.Owner != Stream || EvaluateClaim(fact.Owner, fact.Generation) is not
            (ClaimVerdict.Free or ClaimVerdict.Pin))
            throw new InvalidOperationException("A name claim must be free or re-pinned by its holder.");
        _holder = fact.Owner;
        _heldGeneration = fact.Generation;
    }

    /// <summary>Reducer of the released fact.</summary>
    protected void ObserveReleased(INamedRecordFact fact)
    {
        Acceptance.Observe((DomainEvent)fact);
        if (fact.Operation.Owner != Stream || !IsHeldBy(fact.Owner, fact.Generation))
            throw new InvalidOperationException("A name release must come from the exact holder and generation.");
        _holder = null;
        _heldGeneration = 0;
    }
}

/// <summary>What a claim attempt would meet on a name claim owner.</summary>
public enum ClaimVerdict
{
    /// <summary>Nobody holds the name.</summary>
    Free,

    /// <summary>The same record holds the name from an earlier or equal generation; the claim re-pins it.</summary>
    Pin,

    /// <summary>The same record already pinned the name for a later generation.</summary>
    Superseded,

    /// <summary>Another record holds the name.</summary>
    Taken,
}
