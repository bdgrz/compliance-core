using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

public sealed class CaptureEvidenceArtifactHandler(EvidenceIntake intake, IRequestBus bus)
    : IRequestHandler<CaptureEvidenceArtifact, EvidenceCapture>
{
    public ValueTask<Result<EvidenceCapture>> HandleAsync(
        IRequestContext<CaptureEvidenceArtifact> context, CancellationToken ct)
    {
        var dispatch = new RequestDispatchContext(context.Actor, context.Invocation);
        return CaptureAsync(context.Request, new MemoryStream(context.Request.Content, writable: false), dispatch, ct);
    }

    public async ValueTask<Result<EvidenceCapture>> HandleUploadAsync(CaptureEvidenceArtifact request, Stream content,
        RequestDispatchContext dispatch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(dispatch);

        var authorization = await bus.AuthorizeAsync(request, dispatch, ct).ConfigureAwait(false);
        if (!authorization.IsSuccess)
            return Result<EvidenceCapture>.Failure(authorization.Error);
        return await CaptureAsync(request, content, dispatch, ct).ConfigureAwait(false);
    }

    ValueTask<Result<EvidenceCapture>> CaptureAsync(CaptureEvidenceArtifact request, Stream content,
        RequestDispatchContext dispatch, CancellationToken ct)
    {
        if (dispatch.Invocation is not HttpInvocation || RequestActor.IsSystem(dispatch.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(dispatch.Actor, out var userId))
            return ValueTask.FromResult(Result<EvidenceCapture>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "Evidence capture requires personal HTTP submission.")));

        var collector = ActorReference.ForMember(RbacIds.Member(request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(dispatch.Actor, userId));
        return intake.CaptureAsync(request.TenantId, content, request.Metadata, collector, dispatch, ct);
    }
}
