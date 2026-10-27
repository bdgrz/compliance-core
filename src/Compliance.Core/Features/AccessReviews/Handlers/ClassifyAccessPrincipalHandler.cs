using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Records an explicit classification against governed workforce identities for one exact accepted principal.
///     The principal's own stream serializes its decisions; two principals never contend and the accepted raw
///     population is never touched. A classification before acceptance is rejected. The provider hint shown at the
///     time is retained beside the decision and never decides; an exact retry returns the original frozen decision.
/// </summary>
public sealed class ClassifyAccessPrincipalHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IAccessReviewSources sources, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ClassifyAccessPrincipal, AccessPrincipalClassificationView>
{
    const string CommandKind = "ClassifyAccessPrincipal";

    public async ValueTask<Result<AccessPrincipalClassificationView>> HandleAsync(
        IRequestContext<ClassifyAccessPrincipal> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0 || string.IsNullOrEmpty(request.ProviderSubjectId))
            return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(RequestErrorKind.Validation,
                "A classification requires a command id, a provider subject and a non-negative expected revision.");
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId, request.PopulationId), ct)
            .ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(
                RequestErrorKind.NotFound, "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(
                RequestErrorKind.NotFound, "The population was not found.");

        var actor = AccessReviewActor.From(context);
        var now = clock.GetUtcNow();
        var stream = new AccessPrincipalClassification(request.TenantId, request.PopulationId,
            request.ProviderSubjectId).Stream;
        var provenance = AccessReviewOperation.Provenance(request.CommandId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? decisionContent = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(AccessPrincipalClassification classification,
            CancellationToken token)
        {
            var failure = await GuardAsync(request, population, classification, token).ConfigureAwait(false);
            if (failure.Error is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.Error);
            var decision = new AccessPrincipalClassificationView(request.ExpectedRevision + 1, request.Classification,
                request.PersonId, request.ServiceIdentityId, request.AccountableOwnerPersonId,
                request.SharedJustification?.Trim(), request.Rationale.Trim(), failure.Proposed, actor.Reference, now);
            decisionContent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.DecisionSchema, decision, token).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.DecisionSchema, decisionContent, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(
                AccessReviewContent.OkResponse(AccessReviewContent.DecisionSchema, decisionContent));
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessPrincipalClassification>(binding,
            () => new AccessPrincipalClassification(request.TenantId, request.PopulationId, request.ProviderSubjectId,
                request.CommandId),
            classification => classification.Acceptance,
            PrepareAsync,
            (AccessPrincipalClassification classification, ImmutableContentRef frozen) =>
                classification.Classify(provenance, decisionContent!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessPrincipalClassificationView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<(RequestError? Error, string? Proposed)> GuardAsync(ClassifyAccessPrincipal request,
        AccessPopulation population, AccessPrincipalClassification current, CancellationToken ct)
    {
        if (!population.IsAccepted)
            return (new RequestError(RequestErrorKind.Conflict, "Only an accepted population can be classified."),
                null);
        var facts = await AccessPopulationFactsContent.ReadAsync(content, request.TenantId, population, ct)
            .ConfigureAwait(false);
        var principal = facts.Principals.FirstOrDefault(fact =>
            string.Equals(fact.ProviderSubjectId, request.ProviderSubjectId, StringComparison.Ordinal));
        if (principal is null)
            return (new RequestError(RequestErrorKind.NotFound,
                "The principal was not found in the accepted population."), null);
        if (AccessReviewVocabulary.IsAccessStructure(principal.PrincipalKind))
            return (new RequestError(RequestErrorKind.Validation,
                "Groups and roles are access structures and are not classified."), null);
        foreach (var personId in new[] { request.PersonId, request.AccountableOwnerPersonId })
        {
            if (personId is { } id &&
                !(await reader.HydrateAsync(new Person(request.TenantId, id), ct).ConfigureAwait(false)).IsCreated)
                return (new RequestError(RequestErrorKind.Validation,
                    "The person must be a recorded workforce person in this tenant."), null);
        }

        if (request.ServiceIdentityId is { } serviceIdentityId &&
            !(await reader.HydrateAsync(new ServiceIdentity(request.TenantId, serviceIdentityId), ct)
                .ConfigureAwait(false)).IsCreated)
            return (new RequestError(RequestErrorKind.Validation,
                "The service identity must be governed in this tenant."), null);
        var revision = current.PrepareClassify(request.ExpectedRevision);
        if (!revision.IsSuccess)
            return (revision.Error, null);
        if (AccessClassificationRules.Validate(request.Classification, request.Rationale, request.PersonId,
                request.ServiceIdentityId, request.AccountableOwnerPersonId, request.SharedJustification) is
            { } invalid)
            return (invalid, null);
        var people = await sources.ListPeopleAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!people.IsSuccess)
            return (people.Error, null);
        return (null, AccessPrincipalProposals.Propose(principal, people.Value).Proposal?.Classification);
    }
}
