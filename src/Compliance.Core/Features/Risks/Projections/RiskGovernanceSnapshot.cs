using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Projections;

/// <summary>
///     A program's risk governance composed from the governance, treatment line and action owners of its risks. The
///     query surface is the read model every consumer of the former program-wide ledger uses.
/// </summary>
public sealed class RiskGovernanceSnapshot(Uuid tenantId, Uuid programId,
    IReadOnlyDictionary<Uuid, RiskGovernanceRecord> risks)
{
    /// <summary>The revision of the risk's governance stream; zero when it has no facts.</summary>
    public long RevisionOf(Uuid riskId) => Record(riskId).Revision;

    /// <summary>The current owner of the risk, or null.</summary>
    public RiskOwnerView? OwnerOf(Uuid riskId) => Record(riskId).Owner;

    /// <summary>Whether an independently accepted control treatment is in force.</summary>
    public bool HasAcceptedControlTreatment(Uuid riskId) =>
        Record(riskId).Treatments.Any(static treatment => treatment.Status == RiskStates.TreatmentAccepted);

    /// <summary>The control treatment with this identity, or null.</summary>
    public RiskControlTreatmentView? FindTreatment(Uuid riskId, Uuid treatmentId) =>
        Record(riskId).Treatments.FirstOrDefault(item => item.TreatmentId == treatmentId);

    /// <summary>Every proposed or accepted treatment assertion that names the control.</summary>
    public IReadOnlyList<RiskControlTreatmentView> TreatmentsForControl(Uuid controlId) =>
    [
        .. risks.Values.SelectMany(static risk => risk.Treatments)
            .Where(treatment => treatment.ControlId == controlId &&
                treatment.Status is RiskStates.TreatmentProposed or RiskStates.TreatmentAccepted),
    ];

    /// <summary>Every control-treatment assertion currently awaiting independent review.</summary>
    public IReadOnlyList<RiskControlTreatmentView> PendingControlTreatments() =>
    [
        .. risks.Values.SelectMany(static risk => risk.Treatments)
            .Where(static treatment => treatment.Status == RiskStates.TreatmentProposed),
    ];

    /// <summary>The governance view of the risk with action overdue state and trigger status evaluated at read time.</summary>
    public RiskGovernanceView View(Uuid riskId, IReadOnlyList<RiskAssessmentView> assessments, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        var risk = Record(riskId);
        var actions = risk.Actions.Select(action => action with { Overdue = IsOverdue(action, today) }).ToArray();
        return new RiskGovernanceView(tenantId, programId, riskId, risk.Revision, risk.Owner,
            risk.Treatments.ToArray(), [.. risk.Triggers.Select(trigger => WithStatus(trigger, assessments))], actions,
            ActionStatus(actions));
    }

    /// <summary>Every treatment action in the program, in the order added per risk.</summary>
    public IReadOnlyList<RiskTreatmentActionView> Actions() => [.. risks.Values.SelectMany(static risk => risk.Actions)];

    /// <summary>The action, or null.</summary>
    public RiskTreatmentActionView? FindAction(Uuid riskId, Uuid actionId) =>
        Record(riskId).Actions.FirstOrDefault(item => item.ActionId == actionId);

    /// <summary>The member who submitted the completion, or null.</summary>
    public Uuid? SubmitterMemberId(Uuid riskId, Uuid actionId, Uuid submissionId) =>
        Record(riskId).Submitters.TryGetValue((actionId, submissionId), out var memberId) ? memberId : null;

    /// <summary>Triggers not yet answered by an inherent assessment recorded at or after them.</summary>
    public IReadOnlyList<RiskReassessmentTriggerView> OpenTriggers(Uuid riskId,
        IReadOnlyList<RiskAssessmentView> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        return
        [
            .. Record(riskId).Triggers.Select(trigger => WithStatus(trigger, assessments))
                .Where(static trigger => trigger.Status == "open"),
        ];
    }

    RiskGovernanceRecord Record(Uuid riskId) => risks.GetValueOrDefault(riskId) ?? RiskGovernanceRecord.Empty;

    static bool IsOverdue(RiskTreatmentActionView action, DateOnly today) =>
        action.Status is not (RiskStates.ActionCompleted or RiskStates.ActionCancelled) && action.DueOn < today;

    static string ActionStatus(RiskTreatmentActionView[] actions)
    {
        var active = actions.Where(static action => action.Status != RiskStates.ActionCancelled).ToArray();
        return active.Length == 0 ? "none"
        : active.All(static action => action.Status == RiskStates.ActionCompleted) ? "completed"
        : active.Any(static action => action.Overdue) ? "overdue"
        : "in_progress";
    }

    static RiskReassessmentTriggerView WithStatus(RiskReassessmentTriggerView trigger,
        IReadOnlyList<RiskAssessmentView> assessments) => trigger with
        {
            Status = assessments.Any(assessment => assessment.Phase == RiskEvaluation.Inherent &&
                assessment.AssessedAt >= trigger.RaisedAt)
            ? "resolved"
            : "open",
        };
}
