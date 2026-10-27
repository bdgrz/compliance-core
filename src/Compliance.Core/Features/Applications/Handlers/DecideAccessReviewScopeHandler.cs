using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>Personal effective-dated native scope admission; retries precede mutable instance, owner and waiver reads.</summary>
public sealed class DecideAccessReviewScopeHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<DecideAccessReviewScope, AccessReviewScopeDecisionView>
{
    public async ValueTask<Result<AccessReviewScopeDecisionView>> HandleAsync(
        IRequestContext<DecideAccessReviewScope> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The original scope decision content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessReviewScopeDecisionView>> HandleCoreAsync(
        IRequestContext<DecideAccessReviewScope> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Access-review scope approval requires personal HTTP submission.");
        var request = context.Request;
        if (request.ExpectedSystemInstanceRevision < 1 || request.ExpectedDecisionCount < 0)
            return Failure(RequestErrorKind.Validation,
                "The expected instance revision must be positive and the decision count non-negative.");
        var (memberId, display) = ApplicationActor.From(context);
        var logicalId = SystemInstanceAccessReviewScope.OperationId(request.TenantId, request.ApplicationId, request.SystemInstanceId,
            request.ExpectedDecisionCount);
        var now = clock.GetUtcNow();
        var stream = new SystemInstanceAccessReviewScope(request.TenantId, request.SystemInstanceId).Stream;
        var provenance = RootOperation.Provenance(logicalId, nameof(DecideAccessReviewScope), stream,
            ActorReference.ForMember(memberId, display), request.ExpectedDecisionCount,
            AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = stream };
        ImmutableContentRef? body = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(SystemInstanceAccessReviewScope owner, CancellationToken token)
        {
            try
            {
                return await PrepareCoreAsync(owner, token).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The exact native preparation content cannot be verified.", error is ImmutableContentUnavailableException));
            }
        }

        async ValueTask<Result<FirstResponseDescriptor>> PrepareCoreAsync(SystemInstanceAccessReviewScope owner, CancellationToken token)
        {
            if (owner.Latest is { } prior && prior.ApplicationId != request.ApplicationId)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The system instance was not found."));
            var prepared = owner.PrepareDecide(request.ExpectedDecisionCount, request.EffectiveFrom);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
                request.ApplicationId, request.SystemInstanceId, token).ConfigureAwait(false);
            if (instance is null)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The system instance was not found."));
            if (instance.Revision != request.ExpectedSystemInstanceRevision)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The system instance is at revision {instance.Revision}; the decision names revision {request.ExpectedSystemInstanceRevision}."));
            var application = await reader.HydrateApplicationAsync(request.TenantId, request.ApplicationId, token)
                .ConfigureAwait(false);
            if (application.AccessOwnerPersonId is { } ownerId)
            {
                var person = await reader.HydrateAsync(new Person(request.TenantId, ownerId), token).ConfigureAwait(false);
                if (person.IsCreated && person.CorrelatedUserId is { } ownerUserId)
                    instance = instance with { AccessOwnerMemberId = RbacIds.Member(request.TenantId, ownerUserId) };
            }
            SeparationOfDutiesWaiver? waiver = null;
            if (request.SeparationOfDutiesWaiverId is { } waiverId)
                waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                    .ConfigureAwait(false);
            var rules = new SystemInstanceAccessReviewScopeRules(request.TenantId, request.SystemInstanceId);
            var decided = rules.Decide(instance, 0, logicalId, request.Decision, request.Reason,
                request.EffectiveFrom, request.ReviewBy, memberId, display, now, waiver);
            if (!decided.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(decided.Error!);
            var decision = decided.Value with { Sequence = owner.Revision + 1 };
            body = await NativeContent.WriteAsync(content, request.TenantId, AccessReviewScopeReader.DecisionSchema,
                decision, token).ConfigureAwait(false);
            var frozen = await NativeContent.FreezeAsync(content, request.TenantId,
                AccessReviewScopeReader.DecisionSchema, decision, token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<SystemInstanceAccessReviewScope>(AcceptedOperationBinding.Of(provenance),
            () => new SystemInstanceAccessReviewScope(request.TenantId, request.SystemInstanceId, logicalId),
            owner => owner.Acceptance, PrepareAsync,
            (SystemInstanceAccessReviewScope owner, ImmutableContentRef first) => owner.Record(provenance,
                request.ApplicationId, request.ExpectedSystemInstanceRevision, request.EffectiveFrom, body!, first, result!)),
            context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<AccessReviewScopeDecisionView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<AccessReviewScopeDecisionView> Failure(RequestErrorKind kind, string message) =>
        Result<AccessReviewScopeDecisionView>.Failure(new RequestError(kind, message));
}
