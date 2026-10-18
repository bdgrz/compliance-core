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
            request.IncludedRecordIds is null || request.IncludedRecordIds.Count is < 1 or > 256 ||
            request.IncludedRecordIds.Any(id => id == Uuid.Empty) ||
            request.IncludedRecordIds.Distinct().Count() != request.IncludedRecordIds.Count)
            return Invalid("An amendment requires a package, expected revision, and unique bounded source selection.");

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
        var requestedIds = request.IncludedRecordIds.ToHashSet();
        var selection = prior.SelectedContent.Where(item => requestedIds.Contains(item.SourceRecordId))
            .ToArray();
        if (selection.Length != requestedIds.Count)
            return Conflict("An amendment can retain only source records already selected by the current package version.");

        var content = new AuditPackageContent(current.ExaminationId, current.PackageKind,
            prior.SourceSnapshotKind, prior.SourceSnapshotId, prior.SourceSnapshotSha256,
            current.Name, selection, prior.SourcePeriodId);
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
}
