using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

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
