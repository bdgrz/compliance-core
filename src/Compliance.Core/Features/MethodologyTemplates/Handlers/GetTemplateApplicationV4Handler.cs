using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Reads current application progress from its hydrated authoritative ordering owner.</summary>
public sealed class GetTemplateApplicationV4Handler(IAggregateReader reader) : IRequestHandler<GetTemplateApplicationV4, TemplateApplicationProgressView>
{
    public async ValueTask<Result<TemplateApplicationProgressView>> HandleAsync(IRequestContext<GetTemplateApplicationV4> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty)
            return Result<TemplateApplicationProgressView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Application progress requires exact tenant, program and template identities."));
        var owner = await reader.HydrateAsync(new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true), ct)
            .ConfigureAwait(false);
        var current = owner.AppliedVersionFor(owner.CurrentApplicationCommandId);
        if (current is null)
            return Result<TemplateApplicationProgressView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The canonical template application was not found."));
        return Result<TemplateApplicationProgressView>.Success(new TemplateApplicationProgressView(request.TenantId,
            request.ProgramId, request.TemplateId, current.TemplateVersion, current.PreviousVersion, current.CommandId,
            current.AppliedBy, current.EntriesToCreate.ToArray(), current.EntriesToPropose.ToArray(), owner.IsVersionCompleted,
            owner.CompletionFor(current.CommandId)?.Counts));
    }
}
