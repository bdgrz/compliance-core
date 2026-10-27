using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ListMethodologyTemplatesHandler(IMethodologyTemplateDirectoryReader directory,
    MethodologyTemplateDirectoryReadConsistency consistency, MethodologyTemplateReader templates)
    : IRequestHandler<ListMethodologyTemplates, Page<MethodologyTemplateSummaryView>>
{
    public async ValueTask<Result<Page<MethodologyTemplateSummaryView>>> HandleAsync(
        IRequestContext<ListMethodologyTemplates> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 || !MethodologyTemplateCatalogCursor.TryUnwrap(request.TenantId,
            request.ProgramId, request.Cursor, out var cursor))
            return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The methodology page limit or scoped cursor is invalid."));
        var fence = await consistency.CaptureAsync(ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<MethodologyTemplateSummaryView>>.Failure(fence.Error!);
        try
        {
            var identities = await directory.ListAsync(request.Limit ?? 50, cursor, ct).ConfigureAwait(false);
            var summaries = new List<MethodologyTemplateSummaryView>(identities.Items.Count);
            foreach (var identity in identities.Items)
            {
                var current = await templates.PublicationAsync(identity.TemplateId, ct).ConfigureAwait(false);
                var published = current.Version;
                if (published is null)
                    return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A listed publication differs from its native owner."));
                summaries.Add(new MethodologyTemplateSummaryView(request.TenantId, request.ProgramId, identity.TemplateId,
                    published.Content.Name, current.Owner.IsRetired ? "retired" : "active",
                    current.Owner.IsRetired ? current.Owner.UpdatedAt : published.ApprovedAt!.Value));
            }
            var confirmed = await consistency.ConfirmAsync(fence.Value, ct).ConfigureAwait(false);
            return confirmed.IsSuccess
                ? Result<Page<MethodologyTemplateSummaryView>>.Success(new Page<MethodologyTemplateSummaryView>(summaries,
                    MethodologyTemplateCatalogCursor.Wrap(request.TenantId, request.ProgramId, identities.NextCursor)))
                : Result<Page<MethodologyTemplateSummaryView>>.Failure(confirmed.Error!);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The methodology page cursor is invalid."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology template's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
