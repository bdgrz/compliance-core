using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Work.Aggregates;

/// <summary>Current accountability for one source-derived work item; complete chronology is external verified history.</summary>
public sealed class WorkItemAssignment : Aggregate
{
    public const string Area = "work-item-assignments";
    public const string Claim = "claim";
    public const string Assign = "assign";
    public const string Reassign = "reassign";
    public const string Delegate = "delegate";
    public const string Escalate = "escalate";

    public WorkItemAssignment(Uuid tenantId, Uuid programId, Uuid workItemId, Uuid selectedLogicalId = default)
        : base(IdFor(programId, workItemId), new EventStreamAddress(tenantId.ToString(), Area, IdFor(programId, workItemId).ToString()))
    {
        TenantId = tenantId;
        ProgramId = programId;
        WorkItemId = workItemId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<WorkAssignmentLegacyHistoryImported>(imported =>
        {
            RequireScope(imported.TenantId, imported.ProgramId, imported.WorkItemId);
            if (Revision != 0 || LegacyImport is not null || imported.Revision <= 0 || imported.History is null || imported.ImportedByCommandId == Uuid.Empty)
                throw new InvalidOperationException("Legacy assignment history is imported once into an empty per-item native owner.");
            LegacyImport = imported;
            (Revision, AssigneeMemberId, Escalated) = (imported.Revision, imported.AssigneeMemberId, imported.Escalated);
        });
        On<WorkItemAssignmentRecorded>(ApplyFact);
        On<WorkItemAssignmentEscalated>(ApplyFact);
    }

    public static Uuid IdFor(Uuid programId, Uuid workItemId) => Uuid.CreateVersion5(programId, $"work-item-assignment:{workItemId}");
    public Uuid TenantId { get; }
    public Uuid ProgramId { get; }
    public Uuid WorkItemId { get; }
    public long Revision { get; private set; }
    public Uuid? AssigneeMemberId { get; private set; }
    public bool Escalated { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public WorkAssignmentLegacyHistoryImported? LegacyImport { get; private set; }
    public IWorkAssignmentFact? Latest { get; private set; }
    internal FirmIndependenceReplayCut ReplayCut { get; } = new();

    public Result Record(OperationProvenance operation, WorkAssignmentEntryView entry, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef result, WorkAssignmentLegacyHistoryImported? legacy)
    {
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        var revision = legacy?.Revision ?? Revision;
        if (operation.Owner != Stream || operation.FirstOwner != Stream || operation.LogicalId != operation.RootOperationId ||
            operation.OriginalExpectedRevision != (ulong)revision || entry.Revision != revision + 1 ||
            entry.By != operation.Actor || entry.At != operation.AcceptedAt ||
            operation.CommandKind is not (nameof(AssignWorkItem) or nameof(ClaimWorkItem) or nameof(DelegateWorkItem) or nameof(EscalateWorkItem)) ||
            !WorkAssignmentReader.ActionMatches(operation.CommandKind, entry.Action) ||
            entry.Action != Escalate && entry.AssigneeMemberId is null || entry.Action == Escalate && (legacy?.Escalated ?? Escalated))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The accountability change differs from its exact current revision and immutable native command."));
        if (legacy is not null)
        {
            if (Revision != 0 || legacy.ImportedByCommandId != operation.LogicalId || legacy.TenantId != TenantId ||
                legacy.ProgramId != ProgramId || legacy.WorkItemId != WorkItemId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "A legacy import must belong to this first native operation."));
            RaiseEvent(legacy);
        }
        if (entry.Action == Escalate)
            RaiseEvent(new WorkItemAssignmentEscalated(TenantId, ProgramId, WorkItemId, entry.Revision,
                AssigneeMemberId, true, change, operation, firstResponse, result));
        else
            RaiseEvent(new WorkItemAssignmentRecorded(TenantId, ProgramId, WorkItemId, entry.Revision,
                entry.AssigneeMemberId, Escalated, change, operation, firstResponse, result));
        return Result.Success;
    }

    void ApplyFact<T>(T value) where T : DomainEvent, IWorkAssignmentFact
    {
        RequireScope(value.TenantId, value.ProgramId, value.WorkItemId);
        Acceptance.Observe(value);
        if (value.Operation.Owner != Stream || value.Revision != Revision + 1 || value.FirstResponse is null || value.OperationResult is null ||
            value is WorkItemAssignmentEscalated && (Escalated || !value.Escalated || value.AssigneeMemberId != AssigneeMemberId) ||
            value is WorkItemAssignmentRecorded && (value.AssigneeMemberId is null || value.Escalated != Escalated))
            throw new InvalidOperationException("Native accountability changes preserve exact scope, revision, current assignment and original response.");
        (Revision, AssigneeMemberId, Escalated, Latest) = (value.Revision, value.AssigneeMemberId, value.Escalated, value);
    }

    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
    }

    void RequireScope(Uuid tenantId, Uuid programId, Uuid workItemId)
    {
        if (tenantId != TenantId || programId != ProgramId || workItemId != WorkItemId)
            throw new InvalidOperationException("Work assignment scope differs from its owning native stream.");
    }
}
