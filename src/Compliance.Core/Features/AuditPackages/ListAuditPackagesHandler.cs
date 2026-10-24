using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

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
