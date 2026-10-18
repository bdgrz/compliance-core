using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed class CaptureEvidenceArtifactHandler(EvidenceIntake intake)
    : IRequestHandler<CaptureEvidenceArtifact, EvidenceCapture>
{
    public ValueTask<Result<EvidenceCapture>> HandleAsync(
        IRequestContext<CaptureEvidenceArtifact> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result<EvidenceCapture>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "Evidence capture requires personal HTTP submission.")));

        var request = context.Request;
        var collector = ActorReference.ForMember(RbacIds.Member(request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        var dispatch = new RequestDispatchContext(context.Actor, context.Invocation);
        return intake.CaptureAsync(request.TenantId, new MemoryStream(request.Content, writable: false),
            request.Metadata, collector, dispatch, ct);
    }
}
