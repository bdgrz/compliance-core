using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Admits platform lifecycle decisions before immutable preparation and replays their original Unit outcome.</summary>
public sealed class TenantLifecycleOperations(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, Uuid tenantId, Uuid commandId,
        string kind, string reason, long? expectedRevision, CancellationToken ct) where T : IRequestBase
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A lifecycle decision requires a personal platform operator."));
        if (tenantId == Uuid.Empty || commandId == Uuid.Empty || expectedRevision is < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A lifecycle decision requires its stable owner and command identity."));
        try
        {
            var owner = new Tenant(tenantId);
            var operation = OwnerProvenance.For(owner, commandId, kind,
                ActorReference.ForPlatformOperator(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId)),
                "lifecycle", expectedRevision is { } expected ? (ulong)expected : null,
                AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
            ImmutableContentRef? result = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<Tenant>(AcceptedOperationBinding.Of(operation),
                () => new Tenant(tenantId, commandId), tenant => tenant.Acceptance,
                async (tenant, token) =>
                {
                    var valid = tenant.PrepareLifecycle(kind, reason, expectedRevision);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var response = await UnitResponses.FreezeAsync(content, owner.Stream.Realm, token).ConfigureAwait(false);
                    result = await content.WriteJsonAsync(owner.Stream.Realm, Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, response.Body), token)
                        .ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(response);
                }, (tenant, response) => tenant.ApplyLifecycle(operation, response, result!, reason)), context, ct)
                .ConfigureAwait(false);
            if (accepted.IsSuccess)
            {
                var response = accepted.Value switch
                {
                    AcceptedAppendResult.Committed done => done.Response,
                    AcceptedAppendResult.Replayed replay => replay.Response,
                    _ => null,
                };
                if (response is not null)
                {
                    if (response.ResultSchema != UnitResult.Schema || response.Body is not StandardResponseBody body)
                        throw new ImmutableContentIntegrityException("The lifecycle original response has another schema.");
                    _ = await content.ReadJsonAsync<UnitResult>(owner.Stream.Realm, body.Content, ct).ConfigureAwait(false);
                }
            }
            return UnitResponses.ToResult(accepted);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The lifecycle original response cannot be verified.", exception is ImmutableContentUnavailableException));
        }
    }
}
