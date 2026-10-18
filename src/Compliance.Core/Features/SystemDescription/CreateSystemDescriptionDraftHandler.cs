using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class CreateSystemDescriptionDraftHandler(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<CreateSystemDescriptionDraft>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<CreateSystemDescriptionDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!TryGetAuthor(context, request.TenantId, out var authorMemberId,
                out var authorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Creating a system description requires a personal HTTP actor."));

        var engagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, request.ExaminationId), ct);
        var baseline = engagement.Baseline;
        if (engagement.Current is not { Status: "baseline_frozen" } || baseline is null ||
            baseline.Status is not ("frozen" or "amended") ||
            baseline.TenantId != request.TenantId || baseline.ProgramId != request.ProgramId ||
            baseline.ExaminationEngagementId != request.ExaminationId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A system description draft requires the examination's current frozen Type I baseline."));

        var scope = new SystemDescriptionScope("type_i", request.ExaminationId,
            baseline.TypeIBaselineId, null, null);
        var sources = baseline.Sources.Select(source => new SystemDescriptionSourceReference(
            source.Kind, source.RecordId, source.VersionId, source.Revision,
            source.ContentSha256, "current")).ToArray();
        var content = new SystemDescriptionDraftContent(request.Title, request.Sections,
            sources, [], []);

        return await executor.ExecuteAsync(new SystemDescription(request.TenantId,
                request.ProgramId, request.DescriptionId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Create(context.RequestId,
                    scope, content, authorMemberId, authorDisplay, clock.GetUtcNow())), context, ct);
    }

    static bool TryGetAuthor(IRequestContext<CreateSystemDescriptionDraft> context,
        Uuid tenantId, out Uuid memberId, out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        return context.Invocation is HttpInvocation && !RequestActor.IsSystem(context.Actor) &&
               SystemDescriptionCommandActor.TryGet(context.Actor, tenantId, out memberId,
                   out display);
    }
}
