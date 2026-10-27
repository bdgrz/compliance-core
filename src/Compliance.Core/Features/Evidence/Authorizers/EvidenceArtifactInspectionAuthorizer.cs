using Bdgrz.Compliance.Features.Evidence.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Authorizers;

sealed class EvidenceArtifactInspectionAuthorizer
    : IRequestAuthorizer<RecordEvidenceArtifactInspection>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordEvidenceArtifactInspection> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Artifact inspection results can only be recorded by a trusted system process.")));
}
