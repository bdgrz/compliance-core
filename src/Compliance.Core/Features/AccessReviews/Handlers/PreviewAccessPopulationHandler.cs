using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

public sealed class PreviewAccessPopulationHandler(IAggregateReader reader, IImmutableContentStore content,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<PreviewAccessPopulation, AccessPopulationPreview>
{
    public async ValueTask<Result<AccessPopulationPreview>> HandleAsync(
        IRequestContext<PreviewAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationPreview>(RequestErrorKind.NotFound,
                "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationPreview>(RequestErrorKind.NotFound,
                "The population was not found.");
        var facts = await AccessPopulationFactsContent.ReadAsync(content, request.TenantId, population, ct)
            .ConfigureAwait(false);
        var issues = AccessPopulationAnalysis.Validate(facts);
        return Result<AccessPopulationPreview>.Success(new AccessPopulationPreview(population.Id,
            population.Revision, issues, AccessPopulationAnalysis.Derive(facts),
            issues.Count == 0 && !population.IsAccepted));
    }
}
