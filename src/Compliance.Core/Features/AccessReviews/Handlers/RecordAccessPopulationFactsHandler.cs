using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Replaces a draft population's complete fact set, staged as one immutable content record. The command id is the
///     logical operation identity, so an exact retry returns the original registration even after later changes.
/// </summary>
public sealed class RecordAccessPopulationFactsHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<RecordAccessPopulationFacts, AccessPopulationRegistration>
{
    const string CommandKind = "RecordAccessPopulationFacts";

    public async ValueTask<Result<AccessPopulationRegistration>> HandleAsync(
        IRequestContext<RecordAccessPopulationFacts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "A facts replacement requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        if (!await RestrictedAccessReviewVisibility.CanReadPopulationAsync(reader, visibility,
                request.TenantId, actor.UserId, request.PopulationId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.NotFound, "The population was not found.");
        if (request.Principals is null || request.Entitlements is null || request.GroupMembers is null ||
            request.Assignments is null)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "Every fact list is required; send an empty list when the source has none.");

        var now = clock.GetUtcNow();
        var stream = new AccessPopulation(request.TenantId, request.PopulationId).Stream;
        var provenance = AccessReviewOperation.Provenance(request.CommandId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new AccessPopulation(request.TenantId, request.PopulationId,
            request.CommandId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? facts = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareRecord(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<AccessPopulationRegistration>.Failure(prepared.Error!);
            facts = await AccessReviewContent.WriteAsync(content, request.TenantId, AccessReviewContent.FactsSchema,
                new AccessPopulationFacts(request.Principals, request.Entitlements, request.GroupMembers,
                    request.Assignments), ct).ConfigureAwait(false);
            var body = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.RegistrationSchema,
                new AccessPopulationRegistration(request.PopulationId, request.ExpectedRevision + 1), ct)
                .ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.RegistrationSchema, body, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.RegistrationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessPopulation>(binding,
            () => new AccessPopulation(request.TenantId, request.PopulationId, request.CommandId),
            population => population.Acceptance,
            population => AccessReviewOperation.Prepare(population.PrepareRecord(request.ExpectedRevision), response,
                "The population changed while the facts were prepared; reload and retry."),
            (AccessPopulation population, ImmutableContentRef frozen) => population.Record(provenance, facts!, actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessPopulationRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
