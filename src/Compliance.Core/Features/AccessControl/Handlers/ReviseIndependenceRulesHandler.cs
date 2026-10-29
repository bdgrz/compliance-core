using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ReviseIndependenceRulesHandler(IndependenceRuleCatalogOperations operations)
    : IRequestHandler<ReviseIndependenceRules, IndependenceRuleVersionView>
{
    public ValueTask<Result<IndependenceRuleVersionView>> HandleAsync(IRequestContext<ReviseIndependenceRules> context,
        CancellationToken ct) => operations.ReviseAsync(context, ct);
}
