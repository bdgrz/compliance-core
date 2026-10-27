using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Reports retained operating plans, occurrences, evaluations, corrective work and evidence
///     requests from their authoritative program streams. Cadence plans represent future work;
///     no actor visibility or queue date horizon can hide a retained relationship.
/// </summary>
public sealed class WorkControlImpactContributor(EvidenceRequestReader evidenceRequests,
    FindingReader findings, ControlOperationsReader operatingReader, ControlEvaluationReader evaluationReader)
    : IControlImpactContributor
{
    public string Context => "work";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlReadSnapshot subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var operations = await operatingReader.ReadAsync(subject.TenantId, subject.ProgramId, ct)
            .ConfigureAwait(false);
        var linkedEvaluations = await evaluationReader.ReadControlAsync(subject.TenantId, subject.ProgramId,
            subject.Id, ct).ConfigureAwait(false);
        var remediation = await findings.ReadAsync(subject.TenantId,
            subject.ProgramId, DateTimeOffset.MinValue, ct).ConfigureAwait(false);
        if (!remediation.IsSuccess)
            return Result<ControlImpactContribution>.Failure(remediation.Error);
        var evidence = await evidenceRequests.ReadAsync(subject.TenantId, subject.ProgramId, ct).ConfigureAwait(false);
        var records = new List<ControlAffectedRecord>();
        foreach (var plan in operations.ReadPlans(subject.Id).History)
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_operating_plan", plan.PlanVersionId, plan.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {plan.Status} operating plan (revision {plan.Revision}) retains this Control version and cadence: {plan.CadenceDescription}. Review future work after the decision.")));
        foreach (var occurrence in operations.ReadRecordedOccurrences(subject.Id))
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_occurrence", occurrence.OccurrenceId, occurrence.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {occurrence.State} recorded occurrence (revision {occurrence.Revision}) retains this Control version; preserve its operating history.")));
        foreach (var evaluation in linkedEvaluations)
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_evaluation", evaluation.EvaluationId, evaluation.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {evaluation.State} evaluation (revision {evaluation.Revision}) retains this Control version; review its applicability after the decision.")));
        foreach (var finding in ControlImpactRelationships.Findings(subject, operations,
                     linkedEvaluations, remediation.Value))
        {
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "finding", finding.FindingId, null,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {finding.Status} finding (revision {finding.Revision}) retains a relationship to this Control.")));
            foreach (var action in finding.CorrectiveActions)
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "corrective_action", action.ActionId, null,
                    string.Create(CultureInfo.InvariantCulture,
                        $"The {action.Status} corrective action for finding {finding.FindingId} (revision {finding.Revision}) retains a relationship to this Control.")));
        }
        foreach (var request in evidence.ReadAll().Where(request => request.ControlId == subject.Id))
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "evidence_request", request.EvidenceRequestId, null,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {request.Status} evidence request (revision {request.Revision}) retains collection work for this Control.")));
        return ControlImpactRecords.Contribution(Context, records);
    }
}
