using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetClientIndependenceRulesHandler(IndependenceRuleCatalogReader rules, IndependenceRuleRatificationReader ratifications)
    : IRequestHandler<GetClientIndependenceRules, IReadOnlyList<IndependenceRuleVersionView>>
{
    public async ValueTask<Result<IReadOnlyList<IndependenceRuleVersionView>>> HandleAsync(
        IRequestContext<GetClientIndependenceRules> context, CancellationToken ct)
    {
        try
        {
            var versions = await rules.ReadVersionsAsync(ct).ConfigureAwait(false);
            var decisions = await ratifications.ReadHistoryAsync(ct).ConfigureAwait(false);
            return Result<IReadOnlyList<IndependenceRuleVersionView>>.Success(
                IndependenceRuleVersionProjection.ApplyRatifications(versions, decisions));
        }
        catch (Bdgrz.Compliance.Features.Content.ImmutableContentIntegrityException error)
        {
            return Result<IReadOnlyList<IndependenceRuleVersionView>>.Failure(IndependenceCatalogContent.Failure(error));
        }
    }
}
