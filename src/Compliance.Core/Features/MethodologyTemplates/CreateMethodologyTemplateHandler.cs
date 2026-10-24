using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class CreateMethodologyTemplateHandler(MethodologyTemplateOperations operations) : IRequestHandler<CreateMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplate> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.TemplateId, nameof(CreateMethodologyTemplate),
            null, (owner, member) => owner.PrepareCreate(),
            (rules, member, display, at) => rules.Create(request.Content, request.ChangeNotes, member, display, at), ct);
    }
}
