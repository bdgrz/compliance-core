using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>
///     Reports whether the program's latest readiness assessment relied on any version of the
///     Control. Recorded assessments are immutable, so the decision never rewrites them; it makes
///     the latest result stale until readiness is reassessed. The program readiness owner is the
///     authoritative, lag-free source of the latest adopted assessment.
/// </summary>
public sealed class ReadinessControlImpactContributor(ReadinessReader readiness)
    : IControlImpactContributor
{
    public string Context => "readiness";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlReadSnapshot subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var records = new List<ControlAffectedRecord>();
        try
        {
            var program = await readiness.ReadProgramAsync(subject.TenantId, subject.ProgramId, ct)
                .ConfigureAwait(false);
            if (program.LatestAssessmentId is { } latest &&
                await readiness.ReadContentAsync(subject.TenantId, subject.ProgramId, latest, ct)
                    .ConfigureAwait(false) is { } assessment)
            {
                var versions = subject.ReadVersions().Select(static version => version.VersionId)
                    .ToHashSet();
                var relied = assessment.Findings.SelectMany(static finding => finding.Sources)
                    .Where(source => source.Kind == "control_version" && versions.Contains(source.Id))
                    .Select(static source => source.Id)
                    .Distinct()
                    .ToArray();
                if (relied.Length > 0)
                    records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                        "readiness_assessment", latest, relied[0],
                        "The latest readiness assessment relied on this Control; reassess readiness after the decision."));
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ControlImpactContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The readiness content cannot be verified yet: {exception.Message}", true));
        }

        return ControlImpactRecords.Contribution(Context, records);
    }
}
