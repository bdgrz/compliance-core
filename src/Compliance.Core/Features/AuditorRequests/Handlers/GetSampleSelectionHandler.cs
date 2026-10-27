using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

public sealed class GetSampleSelectionHandler(IAuditorRequestDirectoryReader directory,
    AuditorRequestDirectoryReadConsistency consistency, AuditorRequestReader requests)
    : IRequestHandler<GetSampleSelection, SampleSelectionView>
{
    public ValueTask<Result<SampleSelectionView>> HandleAsync(IRequestContext<GetSampleSelection> context,
        CancellationToken ct) => AuditorRequestRead.ClassifyAsync<SampleSelectionView>(async () =>
        {
            var request = context.Request;
            if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.SampleSelectionId == Uuid.Empty)
                return Fail(AuditorRequestRead.InvalidIdentity());
            var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
            if (!fence.IsSuccess)
                return Fail(fence.Error!);
            var matches = await directory.FindSelectionAsync(request.TenantId, request.ProgramId, request.SampleSelectionId, ct)
                .ConfigureAwait(false);
            if (matches.Count > 2 || matches.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId ||
                row.SampleSelectionId != request.SampleSelectionId || row.AuditorRequestId == Uuid.Empty) ||
                matches.Select(static row => row.AuditorRequestId).Distinct().Count() != matches.Count)
                return Fail(new RequestError(RequestErrorKind.Conflict, "The sample selection locator returned another or duplicate scope."));
            SampleSelectionView? view = null;
            if (matches.Count == 1)
            {
                view = await requests.ReadSelectionAsync(request.TenantId, request.ProgramId, matches[0].AuditorRequestId,
                    request.SampleSelectionId, ct).ConfigureAwait(false);
                if (view is not null && (view.TenantId != request.TenantId || view.ProgramId != request.ProgramId ||
                    view.AuditorRequestId != matches[0].AuditorRequestId || view.SampleSelectionId != request.SampleSelectionId))
                    return Fail(new RequestError(RequestErrorKind.Conflict, "The sample selection source has another exact owner."));
            }
            var confirmed = await consistency.ConfirmAsync(request.TenantId, fence.Value, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Fail(confirmed.Error!);
            if (matches.Count > 1)
                return Fail(new RequestError(RequestErrorKind.Conflict,
                    "The selection identity belongs to more than one request in this program."));
            return matches.Count == 0
                ? Fail(new RequestError(RequestErrorKind.NotFound, "The sample selection was not found."))
                : view is not null ? Result<SampleSelectionView>.Success(view)
                : Fail(new RequestError(RequestErrorKind.Conflict, "The admitted sample selection is not materialized yet.", true));
        });

    static Result<SampleSelectionView> Fail(RequestError error) => Result<SampleSelectionView>.Failure(error);
}
