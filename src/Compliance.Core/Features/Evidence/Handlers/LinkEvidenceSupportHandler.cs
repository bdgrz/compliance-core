using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class LinkEvidenceSupportHandler(EvidenceArtifactDownloadAccess access,
    EvidenceArtifactMetadataRead metadata, EvidenceSupportTargetSource targets,
    IAggregateExecutor executor, TimeProvider clock, IAggregateReader reader)
    : IRequestHandler<LinkEvidenceSupport, EvidenceSupportView>
{
    public async ValueTask<Result<EvidenceSupportView>> HandleAsync(
        IRequestContext<LinkEvidenceSupport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!PersonalHttpActor(context.Actor, context.Invocation, request.TenantId, out var actor))
            return Failure(new RequestError(RequestErrorKind.Forbidden,
                "Evidence support links require a personal HTTP request."));
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0 ||
            request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ArtifactId == Uuid.Empty || request.SupportedRecordId == Uuid.Empty ||
            request.SupportedRecordRevision <= 0 || request.PeriodStart == default ||
            request.PeriodEnd == default || request.PeriodStart > request.PeriodEnd ||
            !BoundedText(request.Purpose) || !BoundedText(request.SufficiencyRationale))
            return Failure(new RequestError(RequestErrorKind.Validation,
                "Evidence support requires a bounded purpose, rationale, exact target revision, and covered period."));

        var allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Failure(allowed.Error);
        var original = await reader.HydrateAsync(new EvidenceSupport(request.TenantId, request.ProgramId,
            request.CommandId), ct).ConfigureAwait(false);
        if (original.Replay(request, actor) is { } retry)
            return retry;
        var captured = await metadata.GetCapturedAsync(request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Failure(captured.Error);
        var source = captured.Value;
        if (source.Metadata.State != EvidenceArtifactStates.Available ||
            source.AvailabilityRecordedAt is not { } availableAt || availableAt > clock.GetUtcNow())
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "Evidence can only be linked after a clean inspection."));
        if (source.Metadata.Content is not { } artifactContent)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "Available evidence must have an authoritative captured coverage period."));
        if (request.PeriodStart < artifactContent.PeriodStart ||
            request.PeriodEnd > artifactContent.PeriodEnd)
            return Failure(new RequestError(RequestErrorKind.Validation,
                "The evidence support period must fit within the artifact's captured coverage period."));
        if (source.Metadata.SourcePosition > long.MaxValue)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact revision cannot be represented safely."));

        var exactTarget = await targets.RequireExactAsync(request, ct).ConfigureAwait(false);
        if (!exactTarget.IsSuccess)
            return Failure(exactTarget.Error);

        allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Failure(allowed.Error);
        var unchanged = await metadata.CheckAsync(source.Metadata, ct).ConfigureAwait(false);
        if (!unchanged.IsSuccess)
            return Failure(unchanged.Error);
        exactTarget = await targets.RequireExactAsync(request, ct).ConfigureAwait(false);
        if (!exactTarget.IsSuccess)
            return Failure(exactTarget.Error);

        var support = new EvidenceSupport(request.TenantId, request.ProgramId,
            EvidenceSupport.IdFor(request));
        var linkedAt = clock.GetUtcNow();
        return await executor.ExecuteAsync(support, aggregate =>
        {
            var failure = aggregate.Link(request, (long)source.Metadata.SourcePosition,
                source.Metadata.ContentSha256, actor, linkedAt);
            var linked = aggregate.Current;
            var view = linked is null
                ? new EvidenceSupportView(request.TenantId, request.ProgramId, support.Id,
                    request.ArtifactId, (long)source.Metadata.SourcePosition,
                    source.Metadata.ContentSha256, request.SupportedRecordType.Trim().ToLowerInvariant(),
                    request.SupportedRecordId, request.SupportedRecordRevision, request.PeriodStart,
                    request.PeriodEnd, request.Purpose.Trim(), request.SufficiencyRationale.Trim(),
                    Uuid.Empty, actor.Display, linkedAt)
                : new EvidenceSupportView(linked.TenantId, linked.ProgramId, linked.SubjectId,
                    linked.ArtifactId, linked.ArtifactRevision, linked.ArtifactContentSha256!,
                    linked.SupportedRecordType!, linked.SupportedRecordId,
                    linked.SupportedRecordRevision, linked.PeriodStart, linked.PeriodEnd,
                    linked.Purpose!, linked.SufficiencyRationale!, linked.ActorMemberId!.Value,
                    linked.ActorDisplay!, linked.OccurredAt);
            return CommandFailureRequestAdapter.ToOutcome(failure, view);
        }, context, ct).ConfigureAwait(false);
    }

    static Result<EvidenceSupportView> Failure(RequestError error) =>
        Result<EvidenceSupportView>.Failure(error);

    static bool PersonalHttpActor(ClaimsPrincipal principal, RequestInvocation invocation,
        Uuid tenantId, out ActorReference actor)
    {
        actor = null!;
        if (invocation is not HttpInvocation || RequestActor.IsSystem(principal) ||
            !UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId))
            return false;
        var display = UserIdentityClaims.BdgrzDisplay(principal, userId);
        if (string.IsNullOrWhiteSpace(display) || tenantId == Uuid.Empty)
            return false;
        actor = ActorReference.ForMember(RbacIds.Member(tenantId, userId), display);
        return true;
    }

    static bool BoundedText(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= 2000;
}
