using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Reports proposed and accepted risk treatment assertions that name the Control. The
///     risk governance owners are the authoritative, lag-free source. An assertion names
///     an exact control version, so approval never silently carries it to a successor.
/// </summary>
public sealed class RiskTreatmentControlImpactContributor(RiskGovernanceReader governance)
    : IControlImpactContributor
{
    public string Context => "risk_treatments";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlReadSnapshot subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var ledger = await governance.ReadAsync(subject.TenantId, subject.ProgramId, ct).ConfigureAwait(false);
        var retirement = !subject.HasOpenDraft;
        var records = ledger.TreatmentsForControl(subject.Id)
            .Select(treatment => new ControlAffectedRecord(subject.TenantId, Context,
                "risk_control_treatment", treatment.TreatmentId, treatment.ControlVersionId,
                (treatment.Status == "accepted"
                    ? "An accepted treatment of risk " + treatment.RiskId
                    : "A proposed treatment of risk " + treatment.RiskId) +
                (retirement
                    ? " relies on this Control; retiring it removes the treatment basis for the residual assessment."
                    : " names this Control version; assert and review the successor version to keep it.")))
            .ToArray();
        return ControlImpactRecords.Contribution(Context, records);
    }
}
