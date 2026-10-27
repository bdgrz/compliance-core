using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Validates the staged raw facts, freezes them through the population snapshot primitive keyed by the command
///     id, then records the attestation. The command id is the logical operation identity, so an exact retry returns the
///     original acceptance and an interrupted freeze resumes with the identical snapshot. Acceptance freezes only the
///     raw observation; classification is a separate owner and never changes it.
/// </summary>
public sealed class AcceptAccessPopulationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    PopulationSnapshotFreezer freezer, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<AcceptAccessPopulation, AccessPopulationAcceptance>
{
    const string CommandKind = "AcceptAccessPopulation";

    public async ValueTask<Result<AccessPopulationAcceptance>> HandleAsync(
        IRequestContext<AcceptAccessPopulation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                "An acceptance requires a command id and a non-negative expected revision.");
        var current = await reader.HydrateAsync(new AccessPopulation(request.TenantId, request.PopulationId,
            request.CommandId), ct).ConfigureAwait(false);
        if (current.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.NotFound,
                "The population was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.NotFound,
                "The population was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewOperation.Provenance(request.CommandId, CommandKind, current.Stream,
            actor.Reference, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        SnapshotRegistration? snapshot = null;
        Uuid calculationId = default;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareAccept(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<AccessPopulationAcceptance>.Failure(prepared.Error!);
            var facts = await AccessPopulationFactsContent.ReadAsync(content, request.TenantId, current, ct)
                .ConfigureAwait(false);
            if (AccessPopulationAnalysis.Validate(facts).Count > 0)
                return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                    "Resolve every population issue before acceptance.");
            if (!AccessReviewVocabulary.IsBoundedText(request.Attestation, 4000))
                return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                    "Acceptance requires an attestation of at most 4000 characters.");

            var header = new AccessPopulationHeader(current.Id, opened.ApplicationId, opened.SystemInstanceId,
                opened.SystemInstanceRevision, opened.ObservedAt, opened.SourceKind, opened.Source,
                request.Attestation.Trim());
            var frozen = await freezer.FreezeAsync(context, request.TenantId, AccessPopulationContent.Kind,
                AccessPopulationContent.Rows(header, facts), null, null, actor.Reference, ct).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<AccessPopulationAcceptance>.Failure(frozen.Error);
            snapshot = frozen.Value;
            calculationId = AccessPopulationAnalysis.CalculationId(snapshot.ContentSha256);
            var body = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.AcceptanceSchema, new AccessPopulationAcceptance(current.Id,
                    snapshot.SnapshotId, snapshot.ContentSha256, calculationId), ct).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.AcceptanceSchema, body, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.AcceptanceSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessPopulation>(binding,
            () => new AccessPopulation(request.TenantId, request.PopulationId, request.CommandId),
            population => population.Acceptance,
            population => AccessReviewOperation.Prepare(population.PrepareAccept(request.ExpectedRevision), response,
                "The population changed while the acceptance was prepared; reload and retry."),
            (AccessPopulation population, ImmutableContentRef frozen) => population.Accept(provenance, snapshot!.SnapshotId, snapshot.ContentSha256,
                calculationId, request.Attestation.Trim(), actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessPopulationAcceptance>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
