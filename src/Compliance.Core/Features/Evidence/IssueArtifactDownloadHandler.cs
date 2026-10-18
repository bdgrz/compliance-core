using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class IssueArtifactDownloadHandler(EvidenceArtifactDownloadAccess access,
    EvidenceArtifactMetadataRead metadata, IArtifactContentStore store,
    IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<IssueArtifactDownload, EvidenceArtifactDownloadLink>
{
    public async ValueTask<Result<EvidenceArtifactDownloadLink>> HandleAsync(
        IRequestContext<IssueArtifactDownload> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!PersonalHttpActor(context.Actor, context.Invocation, request.TenantId, out var actor))
            return Result<EvidenceArtifactDownloadLink>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Artifact downloads require a personal HTTP request."));
        var purpose = request.Purpose?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > EvidenceRules.MaximumTextLength)
            return Result<EvidenceArtifactDownloadLink>.Failure(new RequestError(RequestErrorKind.Validation,
                "A bounded download purpose is required."));

        var allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceArtifactDownloadLink>.Failure(allowed.Error);
        var captured = await metadata.GetCapturedAsync(request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<EvidenceArtifactDownloadLink>.Failure(captured.Error);
        var source = captured.Value;
        if (source.Metadata.State != EvidenceArtifactStates.Available ||
            source.AvailabilityRecordedAt is not { } availableAt || availableAt > clock.GetUtcNow())
            return Result<EvidenceArtifactDownloadLink>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only currently available inspected evidence can be downloaded."));
        if (source.Metadata.SourcePosition > long.MaxValue)
            return Result<EvidenceArtifactDownloadLink>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact revision cannot be represented safely."));

        var content = new ArtifactContentReference(request.TenantId, source.Metadata.ContentSha256,
            source.Metadata.ContentLength);
        var delivery = await store.IssueDeliveryAsync(content, ArtifactContentLimits.MaxDeliveryLifetime, ct)
            .ConfigureAwait(false);
        if (delivery is null || !delivery.Location.IsAbsoluteUri ||
            delivery.Location.Scheme != Uri.UriSchemeHttps &&
            !(delivery.Location.Scheme == "urn" &&
              delivery.Location.OriginalString.StartsWith("urn:bdgrz:artifact-delivery:", StringComparison.Ordinal)) ||
            delivery.ExpiresAt <= clock.GetUtcNow() ||
            delivery.ExpiresAt > clock.GetUtcNow() + ArtifactContentLimits.MaxDeliveryLifetime)
            return Result<EvidenceArtifactDownloadLink>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Verified available content could not be issued for delivery.", isTransient: true));

        allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceArtifactDownloadLink>.Failure(allowed.Error);
        var unchanged = await metadata.CheckAsync(source.Metadata, ct).ConfigureAwait(false);
        if (!unchanged.IsSuccess)
            return Result<EvidenceArtifactDownloadLink>.Failure(unchanged.Error);

        var result = new EvidenceArtifactDownloadLink(delivery.Location, delivery.ExpiresAt);
        return await executor.ExecuteAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId), artifact =>
        {
            if (artifact.CommittedStreamPosition != source.Metadata.SourcePosition ||
                artifact.State != EvidenceArtifactStates.Available ||
                artifact.ContentSha256 != source.Metadata.ContentSha256 ||
                artifact.ContentLength != source.Metadata.ContentLength)
                return AggregateOutcome.Discard(Result<EvidenceArtifactDownloadLink>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "The evidence artifact changed before the download was recorded.", isTransient: true)));
            var failure = artifact.RecordDownload(artifact.Revision, context.RequestId, purpose, actor,
                clock.GetUtcNow(), source.Metadata.ContentSha256, source.Metadata.ContentLength);
            return CommandFailureRequestAdapter.ToOutcome(failure, result);
        }, context, ct).ConfigureAwait(false);
    }

    static bool PersonalHttpActor(System.Security.Claims.ClaimsPrincipal principal,
        RequestInvocation invocation, Uuid tenantId, out ActorReference actor)
    {
        actor = null!;
        if (invocation is not HttpInvocation || RequestActor.IsSystem(principal) ||
            !UserIdentityClaims.TryGetBdgrzSubject(principal, out var user))
            return false;
        actor = ActorReference.ForMember(RbacIds.Member(tenantId, user),
            UserIdentityClaims.BdgrzDisplay(principal, user));
        return true;
    }
}
