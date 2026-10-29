using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>Reads a draft from its stream, or an accepted population from its verified snapshot.</summary>
public sealed class GetAccessPopulationHandler(IAggregateReader reader, IImmutableContentStore content,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessPopulation, AccessPopulationView>
{
    public async ValueTask<Result<AccessPopulationView>> HandleAsync(
        IRequestContext<GetAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var facts = await AccessPopulationFactsContent.ReadAsync(content, request.TenantId, population, ct)
            .ConfigureAwait(false);
        IReadOnlyList<AccessPopulationIssue> issues;
        IReadOnlyList<EffectiveAccessView> effective;
        string? attestation = null;
        if (population.IsAccepted)
        {
            var accepted = await AcceptedAccessPopulation.LoadAsync(reader, content, request.TenantId,
                request.PopulationId, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<AccessPopulationView>.Failure(accepted.Error);
            facts = accepted.Value.Facts;
            effective = accepted.Value.EffectiveAccess;
            attestation = accepted.Value.Header.Attestation;
            issues = [];
        }
        else
        {
            issues = AccessPopulationAnalysis.Validate(facts);
            effective = AccessPopulationAnalysis.Derive(facts);
        }
        var acceptance = population.AcceptedFact;
        return Result<AccessPopulationView>.Success(new AccessPopulationView(request.TenantId,
            population.Id, population.Revision, opened.ApplicationId, opened.SystemInstanceId,
            opened.SystemInstanceRevision, opened.ObservedAt, opened.SourceKind, opened.Source,
            population.Status, facts, issues, effective, acceptance?.CalculationId,
            acceptance?.SnapshotId, acceptance?.ContentSha256, attestation, opened.OpenedBy,
            acceptance?.AcceptedBy, acceptance?.AcceptedAt));
    }
}
