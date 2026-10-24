using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

/// <summary>Native first-owner acceptance over exact immutable local authority and the physical preparation cut.</summary>
public sealed partial class BoundaryOperations(IAggregateReader reader, BoundaryReader boundarys,
    NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock,
    ISeparationOfDutiesWaiverProofResolver? waivers = null)
{
    public IAggregateReader Sources => reader;
    public TimeProvider Clock => clock;

    public async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid? programId, Uuid draftId, Uuid commandId, string kind, long? expectedRevision,
        Func<BoundaryReadSnapshot, BoundaryWriteActor, DateTimeOffset, CancellationToken, ValueTask<Result>> validate,
        Func<BoundaryRules, BoundaryWriteActor, DateTimeOffset, Result> apply,
        Func<BoundaryRules, BoundaryWriteActor, DateTimeOffset, IBoundaryMutationContent?>? receipt,
        Func<IReadOnlyList<SeparationOfDutiesWaiverProof>>? authorizedWaivers, CancellationToken ct,
        string? authoredDigest = null)
        where TRequest : IRequestBase where TOut : notnull
    {
        if (tenantId == Uuid.Empty || draftId == Uuid.Empty || commandId == Uuid.Empty || programId == Uuid.Empty ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation,
                "A boundary operation requires exact identities and a personal member actor."));
        var member = RbacIds.Member(tenantId, userId);
        var display = UserIdentityClaims.BdgrzDisplay(context.Actor, userId);
        if (kind is nameof(AssignResponsibility) or nameof(RevokeResponsibility))
            display = display.Trim();
        var actor = new BoundaryWriteActor(member, display);
        var provenance = OwnerProvenance.For(new SystemBoundary(tenantId, draftId), commandId, kind,
            ActorReference.ForMember(member, actor.Display), draftId.ToString(),
            expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            authoredDigest ?? AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        try
        {
            var preparedSequence = 0L;
            var preparedPhysical = 0UL;
            FrozenBoundaryMutation? frozen = null;
            BoundaryMutationContent? mutation = null;
            BoundaryAuthorityContent? beforeAuthority = null;
            BoundaryAuthorityContent? afterAuthority = null;
            ImmutableContentRef? resultContent = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<SystemBoundary>(
                AcceptedOperationBinding.Of(provenance), () => new SystemBoundary(tenantId, draftId, commandId),
                owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var scope = programId ?? owner.ProgramId;
                    var valid = owner.Prepare(scope, kind == nameof(CreateBoundary));
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var snapshot = await boundarys.ReadAsync(owner, scope, token).ConfigureAwait(false);
                    valid = await validate(snapshot, actor, provenance.AcceptedAt, token).ConfigureAwait(false);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var rules = snapshot.Rules;
                    beforeAuthority = owner.Header is { } header ? rules.Authority(header) : null;
                    valid = apply(rules, actor, provenance.AcceptedAt);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var changes = rules.PendingChanges.ToArray();
                    mutation = new BoundaryMutationContent(changes, authorizedWaivers?.Invoke(),
                        changes.Length == 0 ? receipt?.Invoke(rules, actor, provenance.AcceptedAt) : null);
                    frozen = await BoundaryContentFreezer.FreezeAsync(content, rules, mutation, token).ConfigureAwait(false);
                    afterAuthority = rules.Authority(frozen.Header);
                    var response = await BoundaryResponses.FreezeAsync(content, rules, kind, token).ConfigureAwait(false);
                    resultContent = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(response.ResultSchema, response.Body), token)
                        .ConfigureAwait(false);
                    preparedSequence = owner.NativeSequence;
                    preparedPhysical = owner.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(response);
                },
                (owner, first) => owner.Append(BoundaryFactFactory.Create(kind, frozen!.Header,
                    preparedSequence + 1, provenance, frozen.Change, frozen.Authority, first, resultContent!, mutation!),
                    preparedSequence, preparedPhysical, mutation!, beforeAuthority, afterAuthority!)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<TOut>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The boundary logical operation is already bound to another actor, revision or authored intent."));
            var schema = kind is nameof(CreateBoundary) or nameof(ProposeBoundarySuccessor) ? BoundaryResponses.Registration : UnitResult.Schema;
            if (descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The boundary original response has another result schema.");
            var value = await content.ReadJsonAsync<TOut>(tenantId.ToString(), body.Content, ct).ConfigureAwait(false);
            return Result<TOut>.Success(value);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TOut>.Failure(BoundaryReadFailure.Of(exception));
        }
    }

    public async ValueTask<Result> ExecuteUnitAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid? programId, Uuid draftId, Uuid commandId, string kind, long? expectedRevision,
        Func<BoundaryReadSnapshot, BoundaryWriteActor, DateTimeOffset, CancellationToken, ValueTask<Result>> validate,
        Func<BoundaryRules, BoundaryWriteActor, DateTimeOffset, Result> apply,
        Func<BoundaryRules, BoundaryWriteActor, DateTimeOffset, IBoundaryMutationContent?>? receipt,
        Func<IReadOnlyList<SeparationOfDutiesWaiverProof>>? authorizedWaivers, CancellationToken ct) where TRequest : IRequestBase
    {
        var result = await ExecuteAsync<TRequest, UnitResult>(context, tenantId, programId, draftId, commandId, kind,
            expectedRevision, validate, apply, receipt, authorizedWaivers, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result<SeparationOfDutiesWaiverProof?>> ResolveWaiverAsync(Uuid tenantId, Uuid? waiverId,
        Uuid memberId, Uuid targetId, Uuid targetVersionId, long revision, string action, DateTimeOffset at,
        CancellationToken ct)
    {
        if (waiverId is null)
            return Result<SeparationOfDutiesWaiverProof?>.Success(null);
        if (waivers is null)
            return Result<SeparationOfDutiesWaiverProof?>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact waiver proof resolver is not available.", true));
        var resolved = await waivers.ResolveAsync(new SeparationOfDutiesWaiverProofScope(tenantId, waiverId.Value,
            memberId, memberId, action, SeparationOfDutiesRecordTypes.Boundary, targetId, targetVersionId, revision), at, ct)
            .ConfigureAwait(false);
        return resolved.IsSuccess ? Result<SeparationOfDutiesWaiverProof?>.Success(resolved.Value)
            : Result<SeparationOfDutiesWaiverProof?>.Failure(resolved.Error!);
    }

    public static Result Outcome(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
    public static ValueTask<Result> Ready() => ValueTask.FromResult(Result.Success);
}
