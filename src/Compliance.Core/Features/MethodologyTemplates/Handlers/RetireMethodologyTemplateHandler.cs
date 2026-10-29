using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
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
