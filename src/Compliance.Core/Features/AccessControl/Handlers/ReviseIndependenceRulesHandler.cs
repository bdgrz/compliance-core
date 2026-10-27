using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ReviseIndependenceRulesHandler(IndependenceRuleCatalogOperations operations)
    : IRequestHandler<ReviseIndependenceRules, IndependenceRuleVersionView>
{
    public ValueTask<Result<IndependenceRuleVersionView>> HandleAsync(IRequestContext<ReviseIndependenceRules> context,
        CancellationToken ct) => operations.ReviseAsync(context, ct);
}
