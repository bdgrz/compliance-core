using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed class GetTypeIBaselineHandler(IExaminationEngagementDirectoryReader directory,
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

        var baseline = await directory.GetBaselineAsync(request.TenantId,
            request.TypeIBaselineId, ct).ConfigureAwait(false);
        if (baseline is not { } view || view.TenantId != request.TenantId ||
            view.ProgramId != request.ProgramId || view.TypeIBaselineId != request.TypeIBaselineId)
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
}
