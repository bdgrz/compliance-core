using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class BindSystemDescriptionSourcesHandler(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<BindSystemDescriptionSources>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<BindSystemDescriptionSources> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!TryGetAuthor(context, request.TenantId, out var actorMemberId,
                out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Refreshing system description sources requires a personal HTTP actor."));

        var description = await reader.HydrateAsync(new SystemDescription(request.TenantId,
            request.ProgramId, request.DescriptionId), ct);
        if (description.CurrentVersion is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system description was not found."));
        if (current.Scope.Stage != "type_i" || current.Scope.TypeIBaselineId is not { } baselineId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source refresh requires a Type I description bound to a frozen baseline."));
        if (request.SourceVersionIds is null || request.SourceVersionIds.Count == 0 ||
            request.SourceVersionIds.Any(static id => id == Uuid.Empty) ||
            request.SourceVersionIds.Distinct().Count() != request.SourceVersionIds.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source refresh requires a nonempty list of unique source-version identities."));

        var engagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
            request.ProgramId, current.Scope.ExaminationId), ct);
        var baseline = engagement.Baseline;
        if (engagement.Current is not { Status: "baseline_frozen" } || baseline is null ||
            baseline.Status is not ("frozen" or "amended") ||
            baseline.TypeIBaselineId != baselineId || baseline.ProgramId != request.ProgramId ||
            baseline.TenantId != request.TenantId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The system description's Type I baseline is no longer the examination's current baseline."));

        var requestedVersions = request.SourceVersionIds.ToHashSet();
        var baselineVersions = baseline.Sources.Select(static source => source.VersionId).ToHashSet();
        if (requestedVersions.Count != baselineVersions.Count ||
            !requestedVersions.SetEquals(baselineVersions))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A system description must bind the complete source-version set from its frozen baseline."));
        var selected = baseline.Sources.Where(source => requestedVersions.Contains(source.VersionId))
            .Select(source => new SystemDescriptionSourceReference(source.Kind, source.RecordId,
                source.VersionId, source.Revision, source.ContentSha256, "current")).ToArray();
        if (selected.Length != requestedVersions.Count || selected.Select(static source => source.VersionId)
                .Distinct().Count() != requestedVersions.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Every requested source version must resolve uniquely from the frozen Type I baseline."));

        return await executor.ExecuteAsync(new SystemDescription(request.TenantId,
                request.ProgramId, request.DescriptionId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.BindSources(
                    request.ExpectedRevision, context.RequestId, selected, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }

    static bool TryGetAuthor(IRequestContext<BindSystemDescriptionSources> context,
        Uuid tenantId, out Uuid memberId, out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        return context.Invocation is HttpInvocation && !RequestActor.IsSystem(context.Actor) &&
               SystemDescriptionCommandActor.TryGet(context.Actor, tenantId, out memberId,
                   out display);
    }
}
