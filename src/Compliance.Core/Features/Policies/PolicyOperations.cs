using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Prepares immutable Policy facts and their exact first responses inside the native OCC retry boundary.</summary>
public sealed class PolicyOperations(IAggregateReader reader, PolicyReader policies, NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock, ISeparationOfDutiesWaiverProofResolver? waivers = null)
{
    public IAggregateReader Sources => reader;
    public TimeProvider Clock => clock;

    public async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid tenant,
        Uuid program, Uuid policy, Uuid command, string kind, long? expectedGate,
        Func<PolicyReadSnapshot, PolicyActor, DateTimeOffset, CancellationToken, ValueTask<Result>> validate,
        Func<PolicyRules, PolicyActor, DateTimeOffset, Result> apply, Func<SeparationOfDutiesWaiverProof?>? waiver,
        CancellationToken ct) where TRequest : IRequestBase where TOut : notnull
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || policy == Uuid.Empty || command == Uuid.Empty ||
            RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation,
                "A Policy mutation requires exact identities and an authorized personal member."));
        var actor = PolicyActor.From(context, tenant);
        var operation = OwnerProvenance.For(new Policy(tenant, policy), command, kind,
            actor.Reference, policy.ToString(),
            expectedGate is { } expected ? unchecked((ulong)expected) : null, AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        try
        {
            var sequence = 0L;
            var physical = 0UL;
            FrozenPolicyMutation? frozen = null;
            PolicyMutationContent? mutation = null;
            PolicyAuthorityContent? before = null;
            PolicyAuthorityContent? after = null;
            ImmutableContentRef? resultContent = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<Policy>(AcceptedOperationBinding.Of(operation),
                () => new Policy(tenant, policy, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var valid = owner.Prepare(program, kind == nameof(CreatePolicyDraft));
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var snapshot = await policies.ReadAsync(owner, program, token).ConfigureAwait(false);
                    valid = await validate(snapshot, actor, operation.AcceptedAt, token).ConfigureAwait(false);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var rules = snapshot.Rules;
                    before = owner.Header is { } header ? rules.Authority(header) : null;
                    valid = apply(rules, actor, operation.AcceptedAt);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    if (rules.PendingChanges.Count != 1)
                        throw new ImmutableContentIntegrityException("A successful Policy mutation requires one exact native business change.");
                    mutation = new(rules.PendingChanges[0], waiver?.Invoke());
                    frozen = await PolicyContentStore.FreezeAsync(content, rules, mutation, token).ConfigureAwait(false);
                    after = rules.Authority(frozen.Header);
                    var response = await PolicyResponses.FreezeAsync(content, rules, kind, command, token).ConfigureAwait(false);
                    resultContent = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
                        new MutationResultContent(response.ResultSchema, response.Body), token).ConfigureAwait(false);
                    sequence = owner.NativeSequence;
                    physical = owner.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(response);
                }, (owner, first) => owner.Append(PolicyFactFactory.Create(kind, frozen!.Header, sequence + 1,
                    operation, frozen.Change, frozen.Authority, first, resultContent!, mutation!), sequence, physical,
                    mutation!, before, after!)), context, ct).ConfigureAwait(false);
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
                    "The Policy logical operation is already bound to another actor, gate or authored intent."));
            var schema = PolicyResponses.Schema(kind);
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body ||
                body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The Policy original response has another exact result schema.");
            return Result<TOut>.Success(await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<TOut>.Failure(PolicyReadFailure.Of(failure));
        }
    }

    public async ValueTask<Result<SeparationOfDutiesWaiverProof?>> ResolveWaiverAsync(Uuid tenant, Uuid? waiverId,
        Uuid actor, Uuid policy, long revision, string action, DateTimeOffset at, CancellationToken ct)
    {
        if (waiverId is null)
            return Result<SeparationOfDutiesWaiverProof?>.Success(null);
        if (waivers is null)
            return Result<SeparationOfDutiesWaiverProof?>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact Policy waiver proof resolver is unavailable.", true));
        var proof = await waivers.ResolveAsync(new SeparationOfDutiesWaiverProofScope(tenant, waiverId.Value, actor,
            actor, action, SeparationOfDutiesRecordTypes.Policy, policy, policy, revision), at, ct).ConfigureAwait(false);
        return proof.IsSuccess ? Result<SeparationOfDutiesWaiverProof?>.Success(proof.Value)
            : Result<SeparationOfDutiesWaiverProof?>.Failure(proof.Error!);
    }
    public static Result Outcome(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
    public static ValueTask<Result> Ready() => ValueTask.FromResult(Result.Success);
}
