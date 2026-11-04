using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.SourceFacts.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Resolves directory identity to actual immutable separate-draft publication content.</summary>
public sealed class GetMethodologyTemplateVersionV2Handler(IMethodologyTemplateDirectoryReader directory,
    MethodologyTemplateDirectoryReadConsistency consistency, MethodologyTemplateLineReader templates)
    : IRequestHandler<GetMethodologyTemplateVersionV2, MethodologyTemplatePublishedVersionView>
{
    public async ValueTask<Result<MethodologyTemplatePublishedVersionView>> HandleAsync(
        IRequestContext<GetMethodologyTemplateVersionV2> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MethodologyTemplateVersionId == Uuid.Empty)
            return Result<MethodologyTemplatePublishedVersionView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An exact published methodology version identity is required."));
        var fence = await consistency.CaptureAsync(ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<MethodologyTemplatePublishedVersionView>.Failure(fence.Error!);
        try
        {
            var identity = await directory.FindVersionAsync(request.MethodologyTemplateVersionId, ct).ConfigureAwait(false);
            var version = identity is { SeparateDraftLine: true }
                ? await templates.VersionAsync(identity.TemplateId, identity.VersionNumber, ct).ConfigureAwait(false) : null;
            var confirmed = await consistency.ConfirmAsync(fence.Value, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<MethodologyTemplatePublishedVersionView>.Failure(confirmed.Error!);
            if (identity is not { SeparateDraftLine: true })
                return Result<MethodologyTemplatePublishedVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The published methodology-template version was not found."));
            if (version is null || version.TemplateId != identity.TemplateId || version.Version != identity.VersionNumber ||
                version.VersionId != request.MethodologyTemplateVersionId)
                return Result<MethodologyTemplatePublishedVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The published version identity differs from its native source."));
            return Result<MethodologyTemplatePublishedVersionView>.Success(version);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<MethodologyTemplatePublishedVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology version's immutable source cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
        catch (SourceFactVerificationException)
        {
            return Result<MethodologyTemplatePublishedVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology version source cut cannot be verified. Retry the query.", isTransient: true));
        }
    }
}
