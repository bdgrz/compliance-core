using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
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
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Reports retained evidence requests, fulfilment artifacts and operating support from the
///     authoritative program streams. Unresolved textual support remains represented by its
///     canonical owning record; this preview does not verify artifact existence or content.
/// </summary>
public sealed class EvidenceControlImpactContributor(EvidenceRequestReader evidenceRequests,
    FindingReader findings, ControlOperationsReader operatingReader, ControlEvaluationReader evaluationReader)
    : IControlImpactContributor
{
    public string Context => "evidence";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlReadSnapshot subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var ledger = await evidenceRequests.ReadAsync(subject.TenantId, subject.ProgramId, ct).ConfigureAwait(false);
        var requests = ledger.ReadAll().Where(request => request.ControlId == subject.Id).ToArray();
        var records = requests.Select(request => new ControlAffectedRecord(subject.TenantId,
            Context, "evidence_request", request.EvidenceRequestId, null,
            string.Create(CultureInfo.InvariantCulture,
                $"The {request.Status} evidence request (revision {request.Revision}) names this Control; retain its evidence relationship after the decision.")))
            .ToList();
        var artifacts = new Dictionary<Uuid, SortedSet<string>>();
        foreach (var request in requests.Where(static request => request.ArtifactId is not null))
            AddArtifact(request.ArtifactId!.Value.ToString(),
                $"Fulfilled evidence request {request.EvidenceRequestId} retains this artifact.");
        var operations = await operatingReader.ReadAsync(subject.TenantId, subject.ProgramId, ct)
            .ConfigureAwait(false);
        foreach (var occurrence in operations.ReadRecordedOccurrences(subject.Id))
            foreach (var attestation in occurrence.Attestations.Where(static item => item.Evidence.Count > 0))
            {
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "control_attestation", attestation.AttestationId, attestation.ControlVersionId,
                    string.Create(CultureInfo.InvariantCulture,
                        $"Attestation version {attestation.Version} retains operating support for this Control version, including unresolved references.")));
                AddSupport(attestation.Evidence, $"Attestation {attestation.AttestationId}");
            }
        var linkedEvaluations = await evaluationReader.ReadControlAsync(subject.TenantId, subject.ProgramId,
            subject.Id, ct).ConfigureAwait(false);
        foreach (var evaluation in linkedEvaluations)
        {
            var inspected = evaluation.Steps
                .Concat(evaluation.Submissions.SelectMany(static submission => submission.Steps))
                .SelectMany(static step => step.InspectedItems.Concat(step.Result?.InspectedItems ?? []))
                .Distinct().ToArray();
            if (inspected.Length == 0)
                continue;
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_evaluation", evaluation.EvaluationId, evaluation.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"Evaluation revision {evaluation.Revision} retains its planned and submitted inspected evidence for this Control version.")));
            foreach (var item in inspected.Where(static item => item.Kind == "artifact"))
                AddArtifact(item.Reference,
                    $"Evaluation {evaluation.EvaluationId} inspected artifact version {item.Version}.");
        }
        var remediation = await findings.ReadAsync(subject.TenantId,
            subject.ProgramId, DateTimeOffset.MinValue, ct).ConfigureAwait(false);
        if (!remediation.IsSuccess)
            return Result<ControlImpactContribution>.Failure(remediation.Error);
        foreach (var finding in ControlImpactRelationships.Findings(subject, operations,
                     linkedEvaluations, remediation.Value))
        {
            foreach (var action in finding.CorrectiveActions.Where(static item => item.Evidence is { Count: > 0 }))
            {
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "corrective_action", action.ActionId, null,
                    string.Create(CultureInfo.InvariantCulture,
                        $"Corrective action evidence for linked finding {finding.FindingId} (revision {finding.Revision}) remains retained.")));
                AddSupport(action.Evidence!, $"Corrective action {action.ActionId}");
            }
            foreach (var closure in remediation.Value.ReadClosures(finding.FindingId))
            {
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "finding_closure", closure.DecisionId, null,
                    $"The linked finding {finding.FindingId} retains its closure evidence."));
                AddSupport(closure.ResolutionEvidence, $"Finding closure {closure.DecisionId}");
            }
        }
        records.AddRange(artifacts.Select(artifact => new ControlAffectedRecord(subject.TenantId,
            Context, "evidence_artifact", artifact.Key, null, string.Join(' ', artifact.Value))));
        return ControlImpactRecords.Contribution(Context, records);

        void AddSupport(IReadOnlyList<EvidenceReference> support, string source)
        {
            foreach (var reference in support.Where(static item => item.Kind == "artifact"))
                AddArtifact(reference.Reference, source + " retains this artifact reference.");
        }

        void AddArtifact(string reference, string reason)
        {
            if (!Uuid.TryParse(reference, null, out var id) || id == Uuid.Empty)
                return;
            if (!artifacts.TryGetValue(id, out var reasons))
                artifacts[id] = reasons = new SortedSet<string>(StringComparer.Ordinal);
            reasons.Add(reason);
        }
    }
}
