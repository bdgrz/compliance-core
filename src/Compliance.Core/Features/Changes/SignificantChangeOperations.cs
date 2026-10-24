using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Identified native operations with verified content preparation inside acceptance recovery.</summary>
public sealed class SignificantChangeOperations(NativeOperationExecutor operations, SignificantChangeReader changes,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<UnitResult>> ExecuteAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid programId, Uuid changeId, Uuid commandId, string kind, long? expectedRevision,
        ActorReference actor, Func<SignificantChangeReadSnapshot, OperationProvenance, CancellationToken, ValueTask<Result>> prepare,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || changeId == Uuid.Empty || commandId == Uuid.Empty || expectedRevision is <= 0)
            return Fail(RequestErrorKind.Validation, "A change operation requires exact scope and command identities.");
        var target = new SignificantChange(tenantId, programId, changeId);
        var operation = OwnerProvenance.For(target, commandId, kind, actor,
            changeId.ToString(), expectedRevision is { } revision ? unchecked((ulong)revision) : null,
            AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow()) with
        { FirstOwner = target.Stream };
        FrozenSignificantChangeMutation? frozen = null;
        ImmutableContentRef? result = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<SignificantChange>(AcceptedOperationBinding.Of(operation),
                () => new SignificantChange(tenantId, programId, changeId, commandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (kind == nameof(RecordSignificantChange) ? owner.Header is not null : owner.Header is null || owner.ProgramId != programId)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(kind == nameof(RecordSignificantChange) ? RequestErrorKind.Conflict : RequestErrorKind.NotFound,
                                "The significant change is not available for this operation."));
                        if (expectedRevision is { } expected && owner.Revision != expected)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The significant change requires its original expected revision."));
                        var snapshot = await changes.ReadAsync(owner, programId, token).ConfigureAwait(false);
                        var before = snapshot.View;
                        var ready = await prepare(snapshot, operation, token).ConfigureAwait(false);
                        if (!ready.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                        frozen = await SignificantChangeContentStore.FreezeAsync(content, before, snapshot.Rules, kind, token).ConfigureAwait(false);
                        var response = await NativeContent.FreezeUnitAsync(content, tenantId, token).ConfigureAwait(false);
                        result = response.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(response.Response);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(ContentFailure(exception));
                    }
                }, (owner, response) => owner.Append(SignificantChangeContentStore.Fact(frozen!, sequence + 1, operation, response, result!),
                    frozen!, sequence, position)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<UnitResult>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<UnitResult>.Failure(AcceptedAppendResult.ConflictError);
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != UnitResult.Schema ||
                descriptor.Body is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
                throw new ImmutableContentIntegrityException("The original change response has another result contract.");
            return await NativeContent.ResponseAsync<UnitResult>(content, tenantId, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<UnitResult>.Failure(ContentFailure(exception));
        }
    }
    public static Result Outcome(Bdgrz.Compliance.Features.Versioning.CommandFailure? failure) =>
        failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
    public static RequestError ContentFailure(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The significant change's exact immutable content cannot be verified.", exception is ImmutableContentUnavailableException);
    static Result<UnitResult> Fail(RequestErrorKind kind, string message) => Result<UnitResult>.Failure(new RequestError(kind, message));
}
