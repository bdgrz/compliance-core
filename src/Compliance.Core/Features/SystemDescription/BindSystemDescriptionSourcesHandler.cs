using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class BindSystemDescriptionSourcesHandler(IAggregateReader reader,
    IAggregateExecutor executor, TypeIIPeriodReader periods, TimeProvider clock) : IRequestHandler<BindSystemDescriptionSources>
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
        if (request.SourceVersionIds is null || request.SourceVersionIds.Count == 0 ||
            request.SourceVersionIds.Any(static id => id == Uuid.Empty) ||
            request.SourceVersionIds.Distinct().Count() != request.SourceVersionIds.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source refresh requires a nonempty list of unique source-version identities."));

        IReadOnlyList<SystemDescriptionSourceReference> availableSources;
        if (current.Scope.Stage == "type_i" && current.Scope.TypeIBaselineId is { } baselineId)
        {
            var engagement = await reader.HydrateAsync(new ExaminationEngagement(request.TenantId,
                request.ProgramId, current.Scope.ExaminationId), ct);
            var baseline = engagement.Baseline;
            if (engagement.Current is not { Status: "baseline_frozen" } || baseline is null ||
                baseline.Status is not ("frozen" or "amended") ||
                baseline.TypeIBaselineId != baselineId || baseline.ProgramId != request.ProgramId ||
                baseline.TenantId != request.TenantId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system description's Type I baseline is no longer the examination's current baseline."));

            availableSources = baseline.Sources.Select(source =>
                new SystemDescriptionSourceReference(source.Kind, source.RecordId, source.VersionId,
                    source.Revision, source.ContentSha256, "current")).ToArray();
        }
        else if (current.Scope.Stage == "type_ii" &&
                 current.Scope.TypeIBaselineId is { } typeIBaselineId &&
                 current.Scope.TypeIIPeriodId is { } periodId &&
                 current.Scope.CloseSnapshotId is { } closeSnapshotId)
        {
            var periodRead = await periods.TryReadAsync(request.TenantId, request.ProgramId, periodId, ct).ConfigureAwait(false);
            if (!periodRead.IsSuccess)
                return Result.Failure(periodRead.Error!);
            var period = periodRead.Value.View;
            if (period is not { Status: "closed" } || period.TenantId != request.TenantId ||
                period.ProgramId != request.ProgramId || period.ExaminationId != current.Scope.ExaminationId ||
                period.CloseSnapshots.Count == 0 ||
                period.CloseSnapshots[^1].CloseSnapshotId != closeSnapshotId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system description must bind the current frozen Type II close snapshot."));

            var planAggregate = await reader.HydrateAsync(new TypeIIPlan(request.TenantId,
                request.ProgramId, period.TypeIIPlanId), ct);
            var plan = planAggregate.Current;
            if (plan is not { Status: "approved" } ||
                plan.CurrentVersionNumber != period.TypeIIPlanVersion ||
                plan.Current is not { } approvedVersion ||
                approvedVersion.Content.ExaminationId != current.Scope.ExaminationId ||
                approvedVersion.Content.TypeIBaselineId != typeIBaselineId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system description's Type II period is not backed by its exact approved plan and Type I baseline."));

            availableSources = period.CloseSnapshots[^1].SourceReferences.Select(source =>
                new SystemDescriptionSourceReference(source.SourceType, source.RecordId,
                    source.VersionId, source.Version, source.ContentSha256, "current")).ToArray();
        }
        else
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source refresh requires an examination description bound to an exact Type I baseline or Type II close."));
        }

        var requestedVersions = request.SourceVersionIds.ToHashSet();
        var availableVersionIds = availableSources.Select(static source => source.VersionId).ToHashSet();
        if (requestedVersions.Count != availableVersionIds.Count ||
            !requestedVersions.SetEquals(availableVersionIds))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A system description must bind the complete source-version set from its frozen snapshot."));
        var selected = availableSources.Where(source => requestedVersions.Contains(source.VersionId)).ToArray();
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
