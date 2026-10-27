using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Queries;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

public sealed class ListAuditPackagesHandler(IAuditPackageDirectoryReader directory,
    AuditPackageDirectoryReadConsistency consistency, AuditPackageReader packages)
    : IRequestHandler<ListAuditPackages, Page<AuditPackageSummaryView>>
{
    public async ValueTask<Result<Page<AuditPackageSummaryView>>> HandleAsync(IRequestContext<ListAuditPackages> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<AuditPackageSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Limit must be between 1 and 200."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<AuditPackageSummaryView>>.Failure(fence.Error!);
        Page<AuditPackageSummaryView> identities;
        try
        {
            identities = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<AuditPackageSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The audit-package cursor is invalid."));
        }
        if (identities.Items.Any(value => value.TenantId != request.TenantId || value.ProgramId != request.ProgramId))
            return Result<Page<AuditPackageSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The audit-package directory contains another scope."));
        var current = new List<AuditPackageSummaryView>(identities.Items.Count);
        foreach (var identity in identities.Items)
        {
            var package = await AuditPackageRead.GetAsync(packages, request.TenantId, request.ProgramId,
                identity.AuditPackageId, ct).ConfigureAwait(false);
            if (!package.IsSuccess)
                return Result<Page<AuditPackageSummaryView>>.Failure(package.Error!.Kind == RequestErrorKind.NotFound
                    ? new RequestError(RequestErrorKind.Conflict, "A listed audit package is not visible at its source yet.", true)
                    : package.Error);
            var view = package.Value;
            current.Add(new AuditPackageSummaryView(view.TenantId, view.ProgramId, view.AuditPackageId,
                view.Name, view.Status, view.UpdatedAt));
        }
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence.Value, ct)
            .ConfigureAwait(false);
        return confirmed.IsSuccess
            ? Result<Page<AuditPackageSummaryView>>.Success(new Page<AuditPackageSummaryView>(current, identities.NextCursor))
            : Result<Page<AuditPackageSummaryView>>.Failure(confirmed.Error!);
    }
}
