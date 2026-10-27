using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Readers;

/// <summary>
///     Reads the boundary reverse index for one inventory record after confirming the record exists
///     in the tenant and the index has caught up; rows for any other tenant or record hide it.
/// </summary>
static class InventoryBoundaryReferenceReader
{
    public static async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> ListAsync(
        bool recordExists, string noun, string subjectType, Uuid tenantId, Uuid recordId,
        int? limit, string? cursor, IApplicationBoundaryReferenceDirectory directory,
        ApplicationBoundaryReferenceReadConsistency consistency, CancellationToken ct)
    {
        var notFound = Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
            RequestErrorKind.NotFound, $"The {noun} was not found."));
        if (!recordExists)
            return notFound;
        var ready = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(tenantId, subjectType, recordId, limit ?? 50,
                cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, $"The {noun} boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != tenantId ||
                                      item.SubjectType != subjectType ||
                                      item.GovernedRecordId != recordId)
            ? notFound
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }

    public static Result<Page<ApplicationBoundaryReferenceView>>? RejectLimit(int? limit, string noun) =>
        limit is < 1 or > 200
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                $"The {noun} boundary reference list limit must be between 1 and 200."))
            : null;
}
