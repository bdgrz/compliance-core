using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Reads an operator's actual line header without manufacturing an authored draft.</summary>
public sealed class GetMethodologyTemplateLineHandler(MethodologyTemplateLineReader templates)
    : IRequestHandler<GetMethodologyTemplateLine, MethodologyTemplateLineView>
{
    public async ValueTask<Result<MethodologyTemplateLineView>> HandleAsync(IRequestContext<GetMethodologyTemplateLine> context, CancellationToken ct)
    {
        if (context.Request.TemplateId == Uuid.Empty)
            return Result<MethodologyTemplateLineView>.Failure(new RequestError(RequestErrorKind.Validation, "A template identity is required."));
        try
        {
            var line = await templates.ReadAsync(context.Request.TemplateId, ct).ConfigureAwait(false);
            return line is null ? Result<MethodologyTemplateLineView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The methodology template line was not found.")) : Result<MethodologyTemplateLineView>.Success(line);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<MethodologyTemplateLineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology line source cannot be verified.", isTransient: exception is ImmutableContentUnavailableException));
        }
        catch (SourceFactVerificationException)
        {
            return Result<MethodologyTemplateLineView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology line source cut cannot be verified. Retry the query.", isTransient: true));
        }
    }
}
