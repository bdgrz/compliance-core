using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

public sealed class SignManagementRepresentationLetterHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<SignManagementRepresentationLetter>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<SignManagementRepresentationLetter> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A representation-letter signature requires a personal HTTP actor."));
        if (request.SignerMemberId != actorMemberId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A representation letter can only be signed by the authenticated named signer."));
        if (request.SignedAt == default || request.SignedAt > clock.GetUtcNow())
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A representation-letter signature requires a non-future signing time."));

        var letter = await reader.HydrateAsync(new ManagementRepresentationLetter(request.TenantId,
            request.ProgramId, request.LetterId), ct).ConfigureAwait(false);
        if (letter.Current is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The management representation letter was not found."));

        var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            request.SignedArtifactId), ct).ConfigureAwait(false);
        if (!artifact.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The signed letter artifact was not found."));
        if (artifact.State != EvidenceArtifactStates.Available || artifact.ContentSha256 is not { } sha256)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A representation letter can retain only an available inspected signed artifact."));

        return await executor.ExecuteAsync(new ManagementRepresentationLetter(request.TenantId,
                request.ProgramId, request.LetterId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Sign(aggregate.Revision,
                    actorMemberId, request.SignedArtifactId, sha256, request.SignedAt)), context, ct)
            .ConfigureAwait(false);
    }
}
