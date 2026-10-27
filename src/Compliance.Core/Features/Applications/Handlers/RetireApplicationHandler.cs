using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Retires an application, optionally as merged into an active successor, as one native operation on its
///     stream. An exact retry replays the original result after any later change. A merge re-verifies the approved
///     successor relationship and its impact evidence before the first acceptance only.
/// </summary>
public sealed class RetireApplicationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IApplicationChangeImpactReader impactReader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RetireApplication>
{
    const string CommandKind = "RetireApplication";

    public async ValueTask<Result> HandleAsync(IRequestContext<RetireApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ApplicationId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An application retirement requires a command id, tenant and application."));
        if (request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected application revision must not be negative."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var current = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        var provenance = ApplicationInventoryOperations.Provenance(request.CommandId, CommandKind, current.Stream,
            memberId, request.ExpectedRevision, AuthoredIntentDigest.Compute(Canonical(request)), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (request.MergedIntoApplicationId is { } successorId &&
                successorId != request.ApplicationId && successorId != Uuid.Empty)
            {
                var refused = await VerifyMergeAsync(current, request, successorId, userId, ct).ConfigureAwait(false);
                if (refused is not null)
                    return Result.Failure(refused);
            }

            response = await ApplicationInventoryOperations.UnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        // Portia commits one aggregate stream at a time. The retirement event binds the approved successor
        // revision; it does not promise a cross-stream atomic snapshot.
        var outcome = await operations.ExecuteAsync(new NativeOperation<DeclaredApplication>(binding,
            () => Owner(request), app => app.Acceptance,
            app => ApplicationInventoryOperations.Prepared(Prepare(app, request), response),
            (DeclaredApplication app, ImmutableContentRef frozen) => ApplicationInventoryOperations.Of(app.Retire(
                provenance, frozen, request.ExpectedRevision, request.EffectiveAt, request.Reason,
                request.MergedIntoApplicationId, memberId, display, provenance.AcceptedAt))), context, ct)
            .ConfigureAwait(false);
        return ApplicationInventoryOperations.UnitOutcome(outcome);
    }

    async ValueTask<RequestError?> VerifyMergeAsync(DeclaredApplication predecessor, RetireApplication request,
        Uuid successorId, Uuid userId, CancellationToken ct)
    {
        if (!predecessor.IsCreated)
            return new RequestError(RequestErrorKind.NotFound, "The application was not found.");
        var successor = await reader.HydrateApplicationAsync(request.TenantId, successorId, ct).ConfigureAwait(false);
        if (!successor.IsCreated)
            return new RequestError(RequestErrorKind.Validation, "The merged-into application was not found.");
        if (successor.IsRetired)
            return new RequestError(RequestErrorKind.Conflict, "The merged-into application is retired.");
        if (!MatchesCurrentApproval(predecessor, successor, request))
            return new RequestError(RequestErrorKind.Conflict,
                "Merging requires a current approved successor relationship.");
        var assessment = await impactReader.ReadAsync(new PreviewApplicationChange(
            request.TenantId, request.ApplicationId, request.ExpectedRevision, "retire"),
            userId, ct).ConfigureAwait(false);
        if (!assessment.IsSuccess)
            return assessment.Error;
        var approval = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces, successorId)!.Approval!;
        if (!assessment.Value.Complete || assessment.Value.PendingContexts.Count != 0 ||
            !string.Equals(assessment.Value.ImpactDigest, approval.ImpactDigest, StringComparison.Ordinal))
            return new RequestError(RequestErrorKind.Conflict,
                "Retirement is blocked because the approved impact evidence is incomplete or changed.");

        var latestPredecessor = await reader.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        var latestSuccessor = await reader.HydrateApplicationAsync(request.TenantId, successorId, ct)
            .ConfigureAwait(false);
        return !MatchesCurrentApproval(latestPredecessor, latestSuccessor, request) ||
               latestPredecessor.Relationship(ApplicationRelationshipIdentity.Replaces, successorId)?.Revision !=
               predecessor.Relationship(ApplicationRelationshipIdentity.Replaces, successorId)?.Revision
            ? new RequestError(RequestErrorKind.Conflict,
                "The application relationship changed during retirement validation.")
            : null;
    }

    static Result Prepare(DeclaredApplication app, RetireApplication request)
    {
        if (request.MergedIntoApplicationId is { } successorId && successorId != Uuid.Empty &&
            successorId != request.ApplicationId && !MatchesCurrentApproval(app, successorId, request.ExpectedRevision))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Merging requires a current approved successor relationship."));
        return ApplicationInventoryOperations.Of(app.PrepareRetire(request.ExpectedRevision, request.Reason,
            request.MergedIntoApplicationId, out _));
    }

    static bool MatchesCurrentApproval(DeclaredApplication predecessor,
        DeclaredApplication successor, RetireApplication request)
    {
        if (!predecessor.IsCreated || predecessor.IsRetired ||
            predecessor.Revision != request.ExpectedRevision || !successor.IsCreated ||
            successor.IsRetired || request.MergedIntoApplicationId is not { } successorId)
            return false;
        var relationship = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
            successorId);
        return relationship is { Status: "approved", Approval: { } approval } &&
               relationship.SourceApplicationRevision == request.ExpectedRevision &&
               approval.SourceApplicationRevision == request.ExpectedRevision &&
               approval.TargetApplicationRevision == successor.Revision;
    }

    static bool MatchesCurrentApproval(DeclaredApplication predecessor, Uuid successorId,
        long expectedPredecessorRevision)
    {
        var relationship = predecessor.Relationship(ApplicationRelationshipIdentity.Replaces,
            successorId);
        return relationship is { Status: "approved", Approval: { } approval } &&
               relationship.SourceApplicationRevision == expectedPredecessorRevision &&
               approval.SourceApplicationRevision == expectedPredecessorRevision;
    }

    static RetireApplication Canonical(RetireApplication request) => request with { Reason = request.Reason?.Trim() ?? string.Empty };

    static DeclaredApplication Owner(RetireApplication request) =>
        new(request.TenantId, request.ApplicationId, request.CommandId);
}
