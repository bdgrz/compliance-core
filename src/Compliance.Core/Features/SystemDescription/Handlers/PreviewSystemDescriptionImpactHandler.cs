using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class PreviewSystemDescriptionImpactHandler(SystemDescriptionReader descriptions, SystemDescriptionReadConsistency consistency, TimeProvider clock)
    : IRequestHandler<PreviewSystemDescriptionImpact, SystemDescriptionImpactPreview>
{
    public async ValueTask<Result<SystemDescriptionImpactPreview>> HandleAsync(IRequestContext<PreviewSystemDescriptionImpact> context, CancellationToken ct)
    {
        var r = context.Request;
        try
        {
            var read = await descriptions.ReadAsync(r.TenantId, r.ProgramId, r.SystemDescriptionId, ct).ConfigureAwait(false);
            if (read.CurrentVersion is not { } current)
                return Result<SystemDescriptionImpactPreview>.Failure(new RequestError(RequestErrorKind.NotFound, "The system description was not found."));
            var affected = current.Content.Sources.Select(item => item.RecordId).Concat(current.Content.PeriodChanges.Select(item => item.ChangeId))
                .Concat(current.Content.SignificantChangeIds).Concat(read.View.Bindings.Select(item => item.SnapshotId))
                .Concat(new[] { current.Scope.TypeIBaselineId ?? Uuid.Empty, current.Scope.CloseSnapshotId ?? Uuid.Empty })
                .Where(item => item != Uuid.Empty).Distinct().OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
            var result = new SystemDescriptionImpactPreview(r.TenantId, r.ProgramId,
                $"Version {current.Version} references {current.Content.Sources.Count} exact source versions and {current.Content.PeriodChanges.Count} period changes.", affected, clock.GetUtcNow());
            var confirmed = await consistency.ConfirmAsync(read, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<SystemDescriptionImpactPreview>.Success(result) : Result<SystemDescriptionImpactPreview>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<SystemDescriptionImpactPreview>.Failure(SystemDescriptionReadFailure.Of(failure));
        }
    }
}
