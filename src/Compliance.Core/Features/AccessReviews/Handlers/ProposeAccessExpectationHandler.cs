using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Proposes an expectation on its expectation line. A root expectation opens a new line (expected revision zero);
///     a successor joins the line of its approved predecessor, so a predecessor and its successors share one conflict
///     key and two successors racing one predecessor serialize on the line revision. The expectation id is the
///     logical operation identity, so an exact retry returns the original frozen view even after later changes.
/// </summary>
public sealed class ProposeAccessExpectationHandler(NativeOperationExecutor operations,
    AccessExpectationsReader expectations, IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ProposeAccessExpectation, AccessExpectationView>
{
    const string CommandKind = "ProposeAccessExpectation";

    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ProposeAccessExpectation> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessExpectationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact scoped expectation source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessExpectationView>> HandleCoreAsync(
        IRequestContext<ProposeAccessExpectation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectationId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation,
                "An expectation requires its immutable expectation id and a non-negative expected revision.");
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                instance.ApplicationId, instance.Id, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");

        var digest = AuthoredIntentDigest.Compute(request);
        var lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId,
            request.ExpectationId, request.ExpectationId, CommandKind, digest, ct).ConfigureAwait(false);
        if (lineId is null && request.SupersedesExpectationId is { } predecessorId)
            lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId, predecessorId,
                ct: ct).ConfigureAwait(false) ?? Uuid.Empty;
        if (lineId == Uuid.Empty)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The superseded expectation was not found or is not approved.");
        var line = lineId ?? request.ExpectationId;

        var now = clock.GetUtcNow();
        var stream = new AccessExpectation(request.TenantId, line).Stream;
        var provenance = AccessReviewOperation.Provenance(request.ExpectationId, CommandKind, stream,
            actor.Reference, request.ExpectedRevision, digest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? expectationContent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessExpectation>(binding,
            () => new AccessExpectation(request.TenantId, line, request.ExpectationId),
            expectation => expectation.Acceptance,
            async (owner, prepareCt) =>
            {
                var folded = await AccessExpectationLine.ReadAsync(owner, events, content, request.TenantId,
                    prepareCt).ConfigureAwait(false);
                var prepared = owner.PrepareProposal(request.ExpectedRevision, request.SystemInstanceId,
                    request.ExpectationId);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (AccessExpectationRules.ValidateRule(request.RuleKind, request.Parameters) is { } invalid)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation, invalid);
                if (!AccessReviewVocabulary.IsBoundedText(request.Rationale, 4000))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "An expectation requires a rationale of at most 4000 characters.");
                if (request.EffectiveUntil is { } until && until <= request.EffectiveFrom)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "An expectation must end after it takes effect.");
                if (request.SupersedesExpectationId is { } supersedes &&
                    folded.Find(supersedes) is not { Status: AccessExpectation.Approved })
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.NotFound,
                        "The superseded expectation was not found or is not approved.");
                var view = new AccessExpectationView(request.ExpectationId, instance.Id, request.SupersedesExpectationId,
                    request.RuleKind, AccessExpectationRules.Normalize(request.Parameters), request.Rationale.Trim(),
                    request.EffectiveFrom, request.EffectiveUntil, AccessExpectation.Proposed, actor.Reference, now,
                    null, null, null, request.ExpectedRevision + 1);
                expectationContent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.ExpectationSchema, view, prepareCt).ConfigureAwait(false);
                result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                    AccessReviewContent.ExpectationSchema, expectationContent, prepareCt).ConfigureAwait(false);
                response = AccessReviewContent.OkResponse(AccessReviewContent.ExpectationSchema, expectationContent);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessExpectation expectation, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(expectation) != true)
                    return AccessReviewPreparedCut.Changed();
                return expectation.Propose(provenance, request.SystemInstanceId, actor.MemberId,
                expectationContent!, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessExpectationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
