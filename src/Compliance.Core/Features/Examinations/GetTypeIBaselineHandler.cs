using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class GetTypeIBaselineHandler(IExaminationEngagementDirectoryReader directory, IDomainEventReader events,
    IImmutableContentStore? content = null)
    : IRequestHandler<GetTypeIBaseline, TypeIBaselineView>
{
    public async ValueTask<Result<TypeIBaselineView>> HandleAsync(
        IRequestContext<GetTypeIBaseline> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TypeIBaselineId == Uuid.Empty)
            return Result<TypeIBaselineView>.Failure(new RequestError(
                RequestErrorKind.Validation, "Tenant, program, and baseline identities are required."));

        var before = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (await HasPendingSourceAsync(request.TenantId, before, ct).ConfigureAwait(false))
            return BehindSource();
        var matches = await directory.FindBaselinesAsync(request.TenantId, request.ProgramId,
            request.TypeIBaselineId, ct).ConfigureAwait(false);
        if (matches.Count > 2 || matches.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId ||
            row.TypeIBaselineId != request.TypeIBaselineId || row.ExaminationEngagementId == Uuid.Empty) ||
            matches.Select(row => row.ExaminationEngagementId).Distinct().Count() != matches.Count)
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict, "The baseline locator returned invalid owner scope."));
        var after = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (before != after || await HasPendingSourceAsync(request.TenantId, after, ct).ConfigureAwait(false))
            return BehindSource();
        if (matches.Count > 1)
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The baseline identity belongs to more than one examination in the requested program."));
        var baseline = matches.Count == 0 ? null : await directory.GetBaselineAsync(request.TenantId,
            matches[0].ExaminationEngagementId, request.TypeIBaselineId, ct).ConfigureAwait(false);
        if (baseline is not { } view || view.TenantId != request.TenantId ||
            view.ProgramId != request.ProgramId || view.TypeIBaselineId != request.TypeIBaselineId ||
            view.ExaminationEngagementId != matches[0].ExaminationEngagementId)
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type I baseline was not found."));
        if (view.SourceContentManifest is null)
            return Result<TypeIBaselineView>.Success(view);
        if (content is null)
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The frozen source byte proof cannot be verified."));
        try
        {
            var publication = await SnapshotSourcePublicationReader.ReadAsync(content, request.TenantId,
                request.ProgramId, view.ExaminationEngagementId, view.SourceContentManifest, ct).ConfigureAwait(false);
            if (publication.Sources.Count != view.Sources.Count)
                throw new ImmutableContentIntegrityException("The baseline source publication is incomplete.");
            var enriched = new List<TypeIBaselineSourceReference>(view.Sources.Count);
            foreach (var source in view.Sources)
            {
                var physical = publication.Sources.SingleOrDefault(item => item.SourceType == source.Kind &&
                    item.RecordId == source.RecordId && item.VersionId == source.VersionId && item.Version == source.Revision &&
                    item.LogicalSha256 == source.ContentSha256)?.Content;
                if (physical is null)
                    throw new ImmutableContentIntegrityException("The baseline omits an exact physical source proof.");
                enriched.Add(source with { Content = physical });
            }
            return Result<TypeIBaselineView>.Success(view with { Sources = enriched });
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source byte proof is unavailable: " + exception.Message, true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source byte proof does not verify: " + exception.Message, false));
        }
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId, ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(), "examinations"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static Result<TypeIBaselineView> BehindSource() => Result<TypeIBaselineView>.Failure(new RequestError(RequestErrorKind.Conflict,
        "The examination baseline directory changed or has not reached its exact source. Retry the read.", true));
}
