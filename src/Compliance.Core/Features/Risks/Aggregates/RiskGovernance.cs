using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>
///     One risk's owner and reassessment obligations. The logical revision counts the owner assignments and the
///     reassessment triggers of this risk only, so risks, control treatment lines and actions are independent owners
///     and no program-wide ledger revision or cap exists. Semantic trigger deduplication is verified outside this bounded
///     owner at an exact history cut, then fenced through append.
/// </summary>
public sealed class RiskGovernance : Aggregate
{
    /// <summary>Stream area of every risk governance owner (the realm is the tenant).</summary>
    public const string Area = "risk-governance";

    /// <summary>The identity owner type of a risk's governance.</summary>
    public const string OwnerType = "RiskGovernance";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _riskId;
    RiskOwnerAssigned? _owner;
    DomainEvent? _latest;
    long _revision;

    /// <summary>Opens one risk's governance, selecting the logical operation to resolve.</summary>
    public RiskGovernance(Uuid tenantId, Uuid programId, Uuid riskId, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, riskId),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, riskId).ToString()))
    {
        (_tenantId, _programId, _riskId) = (tenantId, programId, riskId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<RiskOwnerAssigned>(Apply);
        On<RiskReassessmentTriggered>(Apply);
    }

    /// <summary>The identity of the governance of a risk.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid riskId) =>
        AggregateIdentity.Create(OwnerType, tenantId, programId, riskId);

    /// <summary>The deterministic stable identity of a reassessment trigger, so a replayed reaction never raises it twice.</summary>
    public static Uuid TriggerIdFor(Uuid programId, Uuid riskId, string triggerKind, string sourceReference) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(programId, "risk-reassessment-trigger"),
            riskId + "|" + triggerKind + "|" + sourceReference);

    /// <summary>The historical deterministic content convention, retained for source/test identity; new writes may use fresh verified blob ids.</summary>
    public static Uuid TriggerContentId(Uuid triggerId) => Uuid.CreateVersion5(triggerId, RiskContent.TriggerSchema);

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The risk this governance belongs to.</summary>
    public Uuid RiskId => _riskId;

    /// <summary>The logical revision: the number of owner assignments and triggers of this risk.</summary>
    public long Revision => _revision;

    /// <summary>The latest owner assignment, or null when no owner was assigned.</summary>
    public RiskOwnerAssigned? Owner => _owner;
    public DomainEvent? Latest => _latest;
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;

    /// <summary>Checks that the owner can be assigned at the expected revision.</summary>
    public Result PrepareAssignOwner(long expectedRevision) => expectedRevision != _revision
        ? Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk governance", _revision))
            .ToRequestError())
        : Result.Success;

    /// <summary>Fences a source-history decision against the exact command owner cut it observed.</summary>
    public Result PrepareTrigger(long expectedRevision, ulong expectedPhysicalPosition) =>
        expectedRevision == Revision && expectedPhysicalPosition == CommittedStreamPosition
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk governance cut changed; retry trigger admission.", true));

    /// <summary>Assigns the owner as the root native fact of its operation.</summary>
    public Result AssignOwner(OperationProvenance operation, ImmutableContentRef ownerContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(ownerContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An owner assignment is a root operation of its own risk governance with an expected revision."));
        var prepared = PrepareAssignOwner((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskOwnerAssigned(_tenantId, _programId, _riskId, _revision + 1, ownerContent, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Raises a reassessment trigger as the root native fact of its operation.</summary>
    public Result RaiseTrigger(OperationProvenance operation, Uuid triggerId, ImmutableContentRef triggerContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult,
        long expectedRevision, ulong expectedPhysicalPosition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(triggerContent);
        if (operation.Owner != Stream || triggerId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A trigger requires its exact risk governance owner and semantic identity."));
        var prepared = PrepareTrigger(expectedRevision, expectedPhysicalPosition);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskReassessmentTriggered(_tenantId, _programId, _riskId, _revision + 1, triggerContent,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(RiskOwnerAssigned ev)
    {
        if (ev.Operation.CommandKind != nameof(AssignRiskOwner) || ev.OwnerContent.Schema != RiskContent.OwnerSchema ||
            ev.FirstResponse is null || ev.OperationResult is null || ev.Operation.Actor.Kind != "member" ||
            !Uuid.TryParse(ev.Operation.Actor.Id, out var member) || member == Uuid.Empty)
            throw new InvalidOperationException("The owner assignment has another exact native kind or content schema.");
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        Acceptance.Observe(ev);
        _owner = ev;
        Advance(ev);
    }

    void Apply(RiskReassessmentTriggered ev)
    {
        if (ev.Operation.CommandKind != nameof(RaiseRiskReassessmentTriggers) || ev.TriggerContent.Schema != RiskContent.TriggerSchema ||
            ev.FirstResponse is null || ev.OperationResult is null || ev.Operation.Actor.Kind != "system_process")
            throw new InvalidOperationException("The reassessment trigger has another exact native kind or content schema.");
        EnsureNext(ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision, ev.Operation);
        Acceptance.Observe(ev);
        Advance(ev);
    }

    void EnsureNext(Uuid tenantId, Uuid programId, Uuid riskId, long revision, OperationProvenance operation)
    {
        if (tenantId != _tenantId || programId != _programId || riskId != _riskId || revision != _revision + 1 ||
            operation.Owner != Stream || (operation.FirstOwner ?? operation.Owner) != Stream ||
            !NativeOwnerHistory.IsOwnRoot(this, operation,
                operation.CommandKind == nameof(AssignRiskOwner) ? nameof(AssignRiskOwner) : nameof(RaiseRiskReassessmentTriggers),
                operation.CommandKind == nameof(AssignRiskOwner) ? (ulong)_revision : null) ||
            operation.CommandKind is not (nameof(AssignRiskOwner) or nameof(RaiseRiskReassessmentTriggers)))
            throw new InvalidOperationException("A risk governance fact follows the previous one at the next revision.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _latest = fact;
    }
}
