using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

public sealed class ListRequestResponsesHandler(AuditorRequestReader requests)
    : IRequestHandler<ListRequestResponses, Page<AuditorRequestResponseView>>
{
    public ValueTask<Result<Page<AuditorRequestResponseView>>> HandleAsync(IRequestContext<ListRequestResponses> context,
        CancellationToken ct) => AuditorRequestRead.ClassifyAsync<Page<AuditorRequestResponseView>>(async () =>
        {
            var request = context.Request;
            if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.AuditorRequestId == Uuid.Empty)
                return Result<Page<AuditorRequestResponseView>>.Failure(AuditorRequestRead.InvalidIdentity());
            if (request.Limit is < 1 or > 200)
                return Result<Page<AuditorRequestResponseView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Limit must be between 1 and 200."));
            var snapshot = await requests.ReadAsync(request.TenantId, request.ProgramId, request.AuditorRequestId, ct)
                .ConfigureAwait(false);
            if (snapshot.View is null)
                return Result<Page<AuditorRequestResponseView>>.Failure(AuditorRequestRead.Missing());
            var responses = await requests.ReadResponseViewsAsync(request.TenantId, request.ProgramId, request.AuditorRequestId, ct)
                .ConfigureAwait(false);
            if (responses.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId ||
                row.AuditorRequestId != request.AuditorRequestId || row.AuditorRequestResponseId == Uuid.Empty || row.SubmittedAt == default) ||
                responses.Select(static row => row.AuditorRequestResponseId).Distinct().Count() != responses.Count)
                return Result<Page<AuditorRequestResponseView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The submitted response source has another or duplicate exact scope."));
            return AuditorRequestResponsePaging.Create(request.TenantId, request.ProgramId, request.AuditorRequestId,
                responses, request.Limit ?? 50, request.Cursor);
        });
}
