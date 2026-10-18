using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Reads one handoff from its owning stream and verifies the requested program scope.</summary>
public sealed class GetAdvisorRequestHandler(IAggregateReader reader)
    : IRequestHandler<GetAdvisorRequest, AdvisorRequestView>
{
    public async ValueTask<Result<AdvisorRequestView>> HandleAsync(
        IRequestContext<GetAdvisorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.AdvisorRequestId == Uuid.Empty)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An advisor handoff read requires tenant, program, and handoff identities."));

        var handoff = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
            request.ProgramId, request.AdvisorRequestId), ct).ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));

        if (handoff.Purpose is not { } purpose || handoff.CreatedBy is not { } createdBy ||
            handoff.CreatedAt is not { } createdAt || handoff.UpdatedAt is not { } updatedAt)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The advisor handoff history is incomplete."));

        return Result<AdvisorRequestView>.Success(new AdvisorRequestView(request.TenantId,
            request.ProgramId, request.AdvisorRequestId, handoff.Selection, purpose,
            handoff.ExpiresOn, handoff.Status, handoff.Revision, createdBy, createdAt,
            updatedAt));
    }
}
