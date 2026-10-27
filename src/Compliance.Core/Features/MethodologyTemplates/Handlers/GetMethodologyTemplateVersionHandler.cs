using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class GetMethodologyTemplateVersionHandler(IMethodologyTemplateDirectoryReader directory,
    MethodologyTemplateDirectoryReadConsistency consistency, MethodologyTemplateReader templates)
    : IRequestHandler<GetMethodologyTemplateVersion, MethodologyTemplateVersionView>
{
    public async ValueTask<Result<MethodologyTemplateVersionView>> HandleAsync(
        IRequestContext<GetMethodologyTemplateVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var fence = await consistency.CaptureAsync(ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<MethodologyTemplateVersionView>.Failure(fence.Error!);
        try
        {
            var identity = await directory.FindVersionAsync(request.MethodologyTemplateVersionId, ct).ConfigureAwait(false);
            var version = identity is null ? null : await templates.VersionAsync(identity.TemplateId,
                identity.VersionNumber, ct).ConfigureAwait(false);
            var confirmed = await consistency.ConfirmAsync(fence.Value, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<MethodologyTemplateVersionView>.Failure(confirmed.Error!);
            if (identity is null)
                return Result<MethodologyTemplateVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The published methodology-template version was not found."));
            if (version is null || version.TemplateId != identity.TemplateId ||
                version.VersionId != request.MethodologyTemplateVersionId || version.Status != "published")
                return Result<MethodologyTemplateVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The publication identity differs from its native owner."));
            return Result<MethodologyTemplateVersionView>.Success(version);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<MethodologyTemplateVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology template's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
