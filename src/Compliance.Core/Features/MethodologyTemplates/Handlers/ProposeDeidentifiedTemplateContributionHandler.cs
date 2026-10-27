using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ProposeDeidentifiedTemplateContributionHandler(MethodologyTemplateContributionOperations operations)
    : IRequestHandler<ProposeDeidentifiedTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeDeidentifiedTemplateContribution> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ContributionId, request.ContributionId,
            nameof(ProposeDeidentifiedTemplateContribution), false, (owner, _) => owner.PreparePropose(),
            (rules, member, display, at) => rules.Propose(request.SourceRecordId, request.SourceVersion,
                request.ProposedContent, request.DeidentificationRationale, member, display, at),
            (owner, operation, member, user, change, response, result) =>
                owner.Propose(operation, member, user, change, response, result), ct);
    }
}
