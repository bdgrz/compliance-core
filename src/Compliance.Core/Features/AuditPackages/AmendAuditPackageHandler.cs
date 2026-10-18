using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Creates a Type I package version from a subset of its exact prior selection.</summary>
public sealed class AmendAuditPackageHandler(IAggregateExecutor executor, IAggregateReader reader,
    AuditPackageSourceBuilder sources, TimeProvider clock) : IRequestHandler<AmendAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AmendAuditPackage> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Audit package amendment requires a personal HTTP actor."));
        if (request.PackageId == Uuid.Empty || request.ExpectedRevision < 0 ||
            (request.Content is null) == (request.IncludedRecordIds is null))
            return Invalid("An amendment requires one complete source selection or a legacy retained-record selection.");
        if (request.IncludedRecordIds is { } includedRecordIds &&
            (includedRecordIds.Count is < 1 or > 256 || includedRecordIds.Any(id => id == Uuid.Empty) ||
             includedRecordIds.Distinct().Count() != includedRecordIds.Count))
            return Invalid("A retained-record amendment requires a unique bounded source selection.");
        if (request.Content is { } requestedContent && !ValidSelection(requestedContent))
            return Invalid("A complete amendment requires a unique bounded frozen source selection.");

        var aggregateId = new AuditPackage(request.TenantId, request.ProgramId, request.PackageId);
        var loaded = await reader.HydrateAsync(aggregateId, ct).ConfigureAwait(false);
        if (loaded.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The audit package was not found in this program."));
        if (loaded.Revision != request.ExpectedRevision)
            return Conflict("The package changed before this amendment was prepared.");

        var prior = current.Versions.SingleOrDefault(version =>
            version.PackageVersion == current.CurrentVersion);
        if (prior is null)
            return Conflict("The current package version is unavailable for amendment.");
        AuditPackageContent content;
        if (request.Content is { } completeContent)
        {
            var expectedSnapshotKind = current.PackageKind switch
            {
                "type_i" => "type_i_baseline",
                "type_ii" => "type_ii_close_snapshot",
                _ => null,
            };
            if (expectedSnapshotKind is null || completeContent.ExaminationId != current.ExaminationId ||
                completeContent.PackageKind != current.PackageKind || completeContent.Name != current.Name ||
                completeContent.SourceSnapshotKind != expectedSnapshotKind ||
                completeContent.SourcePeriodId != prior.SourcePeriodId ||
                prior.SourceSnapshotKind != expectedSnapshotKind)
                return Conflict("An amendment must preserve package identity and use its supported frozen source type and period.");
            content = completeContent;
        }
        else
        {
            var requestedIds = request.IncludedRecordIds!.ToHashSet();
            var selection = prior.SelectedContent.Where(item => requestedIds.Contains(item.SourceRecordId))
                .ToArray();
            if (selection.Length != requestedIds.Count)
                return Conflict("A legacy amendment can retain only source records already selected by the current package version.");

            content = new AuditPackageContent(current.ExaminationId, current.PackageKind,
                prior.SourceSnapshotKind, prior.SourceSnapshotId, prior.SourceSnapshotSha256,
                current.Name, selection, prior.SourcePeriodId);
        }

        var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, content, ct)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result.Failure(resolved.Error);

        return await executor.ExecuteAsync(aggregateId, aggregate =>
            CommandFailureRequestAdapter.ToOutcome(aggregate.Amend(request.ExpectedRevision,
                request.AmendmentReason, resolved.Value.SourceSnapshotId,
                resolved.Value.SourceSnapshotSha256, resolved.Value.SelectedContent,
                actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static bool ValidSelection(AuditPackageContent content) =>
        content.SelectedContent is { Count: >= 1 and <= 256 } selected &&
        selected.All(item => item is not null && item.SourceRecordId != Uuid.Empty) &&
        selected.Select(item => (item.SourceType, item.SourceRecordId, item.SourceVersion))
            .Distinct().Count() == selected.Count;
}
