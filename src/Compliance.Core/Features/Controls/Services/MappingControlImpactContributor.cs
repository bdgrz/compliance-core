using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
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
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Reports the program's active and pending criterion mappings that name the Control. The
///     mapping lines are the authoritative, lag-free source. Approval never remaps them:
///     a mapping to a superseded or retired version must be remapped or retired explicitly.
/// </summary>
public sealed class MappingControlImpactContributor(ControlMappingReader mappings)
    : IControlImpactContributor
{
    public string Context => "mappings";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlReadSnapshot subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var snapshot = await mappings.ReadProgramAsync(subject.TenantId, subject.ProgramId, ct)
            .ConfigureAwait(false);
        var retirement = !subject.HasOpenDraft;
        var records = snapshot.Mappings
            .Where(mapping => mapping.ControlId == subject.Id &&
                mapping.Status is "active" or "pending")
            .Select(mapping => new ControlAffectedRecord(subject.TenantId, Context,
                "control_criterion_mapping", mapping.MappingId,
                mapping.ActiveControlVersionId ?? mapping.Versions[^1].ControlVersionId,
                (mapping.Status == "pending"
                    ? "A pending mapping proposal for " + mapping.CriterionIdentifier
                    : "The accepted mapping to " + mapping.CriterionIdentifier) +
                (retirement
                    ? " names this Control; retire it or map the criterion to another control."
                    : " names this Control; remap it to the successor version after approval.")))
            .ToArray();
        return ControlImpactRecords.Contribution(Context, records);
    }
}
