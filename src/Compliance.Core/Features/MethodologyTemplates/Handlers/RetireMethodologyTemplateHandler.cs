using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class RetireMethodologyTemplateHandler(MethodologyTemplateOperations operations) : IRequestHandler<RetireMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireMethodologyTemplate> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.CommandId, nameof(RetireMethodologyTemplate),
            request.ExpectedRevision, (owner, member) => owner.PrepareRetire(request.ExpectedRevision),
            (rules, member, display, at) => rules.Retire(request.ExpectedRevision, request.EffectiveOn, request.Rationale, member, display, at), ct);
    }
}
