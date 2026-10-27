using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ReviseIndependenceRulesHandler(IndependenceRuleCatalogOperations operations)
    : IRequestHandler<ReviseIndependenceRules, IndependenceRuleVersionView>
{
    public ValueTask<Result<IndependenceRuleVersionView>> HandleAsync(IRequestContext<ReviseIndependenceRules> context,
        CancellationToken ct) => operations.ReviseAsync(context, ct);
}
