using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records the acting member's personal expectation approval on its expectation line; HTTP-only, never an MCP
///     tool. The proposer may approve only under an approved waiver scoped to the exact expectation. The command id
///     is the logical operation identity, so an exact retry returns the original frozen view.
/// </summary>
public sealed class ApproveAccessExpectationHandler(NativeOperationExecutor operations,
    AccessExpectationsReader expectations, IAggregateReader reader, IImmutableContentStore content,
    TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ApproveAccessExpectation, AccessExpectationView>
{
    const string CommandKind = "ApproveAccessExpectation";

    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ApproveAccessExpectation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation,
                "An approval requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                request.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId,
            request.ExpectationId, ct).ConfigureAwait(false);
        if (lineId is not { } line)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The expectation was not found.");

        var now = clock.GetUtcNow();
        var stream = new AccessExpectation(request.TenantId, line).Stream;
        var provenance = AccessReviewOperation.Provenance(request.CommandId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var (owner, folded) = await expectations.ReadLineAsync(request.TenantId, line, request.CommandId, ct)
            .ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        Uuid? waiverId = null;
        if (owner.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = owner.PrepareApproval(request.ExpectedRevision, request.ExpectationId);
            if (!prepared.IsSuccess)
                return Result<AccessExpectationView>.Failure(prepared.Error!);
            SeparationOfDutiesWaiver? waiver = null;
            if (request.SeparationOfDutiesWaiverId is { } requested)
                waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, requested), ct)
                    .ConfigureAwait(false);
            var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.AccessExpectation,
                request.ExpectationId, request.ExpectationId, 1, SeparationOfDutiesActions.Approve);
            var conflict = owner.Proposers[request.ExpectationId] == actor.MemberId;
            if (waiver is not null && (!conflict || waiver.TenantId != request.TenantId ||
                                       !waiver.Allows(scope, actor.MemberId, now)))
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                    "The separation-of-duties waiver is not active for this member and expectation.");
            if (conflict && waiver is null)
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                    "The member who proposed an expectation cannot approve it.");
            waiverId = waiver?.Id;
            var approved = folded.Find(request.ExpectationId)! with
            {
                Status = AccessExpectation.Approved,
                ApprovedBy = actor.Reference,
                ApprovedAt = now,
                SeparationOfDutiesWaiverId = waiverId,
                Revision = request.ExpectedRevision + 1,
            };
            var body = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.ApprovedExpectationSchema, approved, ct).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.ApprovedExpectationSchema, body, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.ApprovedExpectationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessExpectation>(binding,
            () => new AccessExpectation(request.TenantId, line, request.CommandId),
            expectation => expectation.Acceptance,
            expectation => AccessReviewOperation.Prepare(expectation.PrepareApproval(request.ExpectedRevision,
                request.ExpectationId), response,
                "The expectation line changed while the approval was prepared; reload and retry."),
            (AccessExpectation expectation, ImmutableContentRef frozen) => expectation.Approve(provenance, request.ExpectationId, actor.Reference, now,
                waiverId, frozen, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessExpectationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
