using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class AcceptTemplateContributionHandler(MethodologyTemplateContributionOperations operations)
    : IRequestHandler<AcceptTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AcceptTemplateContribution> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ContributionId, request.CommandId,
            nameof(AcceptTemplateContribution), true, (owner, user) => owner.PrepareAccept(user),
            (rules, member, display, at) => rules.Accept(rules.Revision, request.AcceptanceRationale, member, display, at),
            (owner, operation, member, user, change, response, result) =>
                owner.Accept(operation, member, user, change, response, result), ct);
    }
}
