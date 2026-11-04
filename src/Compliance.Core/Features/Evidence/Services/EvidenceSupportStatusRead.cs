using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;

namespace Bdgrz.Compliance.Features.Evidence.Services;

sealed class EvidenceSupportStatusRead(IAggregateReader reader, IDomainEventReader events,
    EvidenceArtifactReadAccess access)
{
    public ValueTask<Result<EvidenceSupportStatusView>> GetAsync(IRequestContext context, Uuid tenantId,
        Uuid programId, Uuid supportId, CancellationToken ct) => ValueTask.FromResult(
            Result<EvidenceSupportStatusView>.Failure(new RequestError(RequestErrorKind.NotFound, "Not implemented.")));

    public ValueTask<Result<Page<EvidenceSupportStatusView>>> ListAsync(IRequestContext context,
        ListEvidenceSupports request, CancellationToken ct) => ValueTask.FromResult(
            Result<Page<EvidenceSupportStatusView>>.Success(new([], null)));
}
