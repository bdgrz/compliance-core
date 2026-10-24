using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Native root admission for independently owned retention decisions; authorization stays in the request pipeline.</summary>
sealed class ArtifactRetentionMutation(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, IDomainEventReader events)
{
    public ValueTask<Result> RecordBasisAsync(IRequestContext<RecordArtifactRetentionBasis> context, CancellationToken ct) =>
        Personal(context, actor => RecordBasisAsync(context, context.Request, actor, ct));

    // Generated exports already prove their personal actor and exact frozen source. Preserve that existing policy
    // while moving their basis to the same native owner; the separate export process remains a later slice.
    internal ValueTask<Result> RecordBasisAsync(IExecutionContext context, RecordArtifactRetentionBasis request,
        ActorReference actor, CancellationToken ct) => ExecuteAsync(context, request, request.TenantId,
        request.SourceKind, request.SourceId, request.ExpectedContentSha256, request.CommandId,
        "RecordArtifactRetentionBasis", request.ExpectedRevision, actor,
        () => new ArtifactRetentionBasis(new(request.TenantId, request.SourceKind, request.SourceId,
            request.ExpectedContentSha256), request.CommandId), basis => basis.Acceptance,
        (basis, source, _) => basis.PrepareRecord(source, request.ExpectedRevision,
            request.PeriodStart, request.PeriodEnd, request.Reason),
        (basis, source, operation, first, result) => basis.Record(operation, source,
            request.PeriodStart, request.PeriodEnd, request.Reason, first, result), ct);

    public ValueTask<Result> PlaceAsync(IRequestContext<PlaceArtifactLegalHold> context, CancellationToken ct) =>
        Personal(context, actor =>
        {
            var request = context.Request;
            return ExecuteAsync(context, request, request.TenantId, request.SourceKind, request.SourceId,
                request.ExpectedContentSha256, request.HoldId, "PlaceArtifactLegalHold", request.ExpectedRevision, actor,
                () => new ArtifactLegalHold(request.TenantId, request.HoldId, request.HoldId), hold => hold.Acceptance,
                (hold, source, _) => hold.PreparePlace(source, request.ExpectedRevision, request.Reason),
                (hold, source, operation, first, result) => hold.Place(operation, source, request.Reason, first, result), ct);
        });

    public ValueTask<Result> ReleaseAsync(IRequestContext<ReleaseArtifactLegalHold> context, CancellationToken ct) =>
        Personal(context, actor =>
        {
            var request = context.Request;
            return ExecuteAsync(context, request, request.TenantId, request.SourceKind, request.SourceId,
                request.ExpectedContentSha256, request.CommandId, "ReleaseArtifactLegalHold", request.ExpectedRevision, actor,
                () => new ArtifactLegalHold(request.TenantId, request.HoldId, request.CommandId), hold => hold.Acceptance,
                (hold, source, at) => hold.PrepareRelease(source, request.ExpectedRevision, request.Reason, at),
                (hold, source, operation, first, result) => hold.ReleaseHold(operation, source, request.Reason, first, result), ct);
        });

    static ValueTask<Result> Personal<T>(IRequestContext<T> context, Func<ActorReference, ValueTask<Result>> next)
        where T : IArtifactRetentionAdminRequest
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Retention decisions require a personal Bdgrz HTTP invocation.")));
        return next(ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user)));
    }

    async ValueTask<Result> ExecuteAsync<TRequest, TOwner>(IExecutionContext context, TRequest request,
        Uuid tenant, string kind, Uuid sourceId, string expectedSha, Uuid logicalId, string commandKind,
        long expectedRevision, ActorReference actor, Func<TOwner> hydrate, Func<TOwner, NativeAcceptance> acceptance,
        Func<TOwner, ArtifactRetentionSourceSnapshot, DateTimeOffset, Result> prepare,
        Func<TOwner, ArtifactRetentionSourceSnapshot, OperationProvenance, ImmutableContentRef, ImmutableContentRef, Result> apply,
        CancellationToken ct) where TRequest : notnull where TOwner : Aggregate
    {
        var identity = new ArtifactRetentionSource(tenant, kind, sourceId, expectedSha);
        if (logicalId == Uuid.Empty || expectedRevision < 0 || !ArtifactRetentionRules.ValidSource(identity))
            return ArtifactRetentionRules.Invalid("A retention decision requires its exact typed identity, digest and non-negative owner revision.");
        var owner = hydrate();
        var at = clock.GetUtcNow();
        var operation = FindingContent.Provenance(logicalId, commandKind, owner.Stream, actor,
            (ulong)expectedRevision, AuthoredIntentDigest.Compute(request), at);
        ArtifactRetentionSourceSnapshot? selected = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<TOwner>(AcceptedOperationBinding.Of(operation),
            hydrate, acceptance, async (aggregate, token) =>
            {
                var legacy = await ArtifactRetentionLegacyGuard.RequireNoLegacyAsync(events, identity, token).ConfigureAwait(false);
                if (!legacy.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(legacy.Error!);
                var source = await ArtifactRetentionSourceReader.LoadAsync(reader, tenant, kind, sourceId, token)
                    .ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(source.Error!);
                if (source.Value.Source != identity)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The retained source content differs from the authored exact digest."));
                var valid = prepare(aggregate, source.Value, at);
                if (!valid.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, token).ConfigureAwait(false);
                if (!fence.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(fence.Error!);
                selected = source.Value;
                var frozen = await FindingContent.FreezeAsync(content, tenant, UnitResult.Schema, new UnitResult(), token)
                    .ConfigureAwait(false);
                result = frozen.Result;
                return Result<FirstResponseDescriptor>.Success(frozen.Response);
            }, (aggregate, first) => apply(aggregate, selected!, operation, first, result!)), context, ct).ConfigureAwait(false);
        var response = await FindingContent.ResponseAsync<UnitResult>(content, tenant, outcome, ct).ConfigureAwait(false);
        return response.IsSuccess ? Result.Success : Result.Failure(response.Error!);
    }
}
