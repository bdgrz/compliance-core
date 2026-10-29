using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetIndependenceRuleVersionsHandler(IndependenceRuleCatalogReader rules, IndependenceRuleRatificationReader ratifications)
    : IRequestHandler<GetIndependenceRuleVersions, IReadOnlyList<IndependenceRuleVersionView>>
{
    public async ValueTask<Result<IReadOnlyList<IndependenceRuleVersionView>>> HandleAsync(
        IRequestContext<GetIndependenceRuleVersions> context, CancellationToken ct)
    {
        try
        {
            var versions = await rules.ReadVersionsAsync(ct).ConfigureAwait(false);
            var decisions = await ratifications.ReadHistoryAsync(ct).ConfigureAwait(false);
            return Result<IReadOnlyList<IndependenceRuleVersionView>>.Success(
                IndependenceRuleVersionProjection.ApplyRatifications(versions, decisions));
        }
        catch (Bdgrz.Compliance.Features.Content.Services.ImmutableContentIntegrityException error)
        {
            return Result<IReadOnlyList<IndependenceRuleVersionView>>.Failure(IndependenceCatalogContent.Failure(error));
        }
    }
}
