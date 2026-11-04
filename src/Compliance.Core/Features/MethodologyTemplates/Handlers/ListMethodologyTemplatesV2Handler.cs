using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.SourceFacts.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Lists published separate-draft templates using only directory discovery and verified native line content.</summary>
public sealed class ListMethodologyTemplatesV2Handler(IMethodologyTemplateDirectoryReader directory,
    MethodologyTemplateDirectoryReadConsistency consistency, MethodologyTemplateLineReader templates)
    : IRequestHandler<ListMethodologyTemplatesV2, Page<MethodologyTemplateSummaryView>>
{
    public async ValueTask<Result<Page<MethodologyTemplateSummaryView>>> HandleAsync(
        IRequestContext<ListMethodologyTemplatesV2> context, CancellationToken ct)
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
            var summaries = new List<MethodologyTemplateSummaryView>();
            foreach (var identity in identities.Items.Where(identity => identity.SeparateDraftLine))
            {
                var publication = await templates.PublicationAsync(identity.TemplateId, ct).ConfigureAwait(false);
                if (publication?.Version is null || publication.Line.PublishedVersion != publication.Version.Version)
                    return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A listed methodology publication differs from its native source."));
                summaries.Add(new MethodologyTemplateSummaryView(request.TenantId, request.ProgramId, identity.TemplateId,
                    publication.Line.Name, publication.Line.Status == "retired" ? "retired" : "active", publication.Line.UpdatedAt));
            }
            var confirmed = await consistency.ConfirmAsync(fence.Value, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<Page<MethodologyTemplateSummaryView>>.Success(new Page<MethodologyTemplateSummaryView>(summaries,
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
                "The methodology publication's immutable source cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
        catch (SourceFactVerificationException)
        {
            return Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology publication source cut cannot be verified. Retry the query.", isTransient: true));
        }
    }
}
