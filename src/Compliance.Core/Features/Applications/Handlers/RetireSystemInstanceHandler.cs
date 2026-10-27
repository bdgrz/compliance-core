using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Retires one registered system instance without removing its scope history, as one native operation on the
///     instance's stream. An exact retry replays the original result after any later change.
/// </summary>
public sealed class RetireSystemInstanceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RetireSystemInstance>
{
    const string CommandKind = "RetireSystemInstance";

    public async ValueTask<Result> HandleAsync(IRequestContext<RetireSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty ||
            request.ApplicationId == Uuid.Empty || request.SystemInstanceId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A system instance retirement requires a command id, tenant, application and instance."));
        if (request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected system instance revision must not be negative."));
        var current = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        var provenance = ApplicationInventoryOperations.Provenance(request.CommandId, CommandKind, current.Stream,
            memberId, request.ExpectedRevision, AuthoredIntentDigest.Compute(Canonical(request)), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var invalid = Prepare(current, request, memberId, display, provenance.AcceptedAt);
            if (!invalid.IsSuccess)
                return invalid;
            response = await ApplicationInventoryOperations.UnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<DeclaredSystemInstance>(binding,
            () => Owner(request), instance => instance.Acceptance,
            instance => ApplicationInventoryOperations.Prepared(
                Prepare(instance, request, memberId, display, provenance.AcceptedAt), response),
            (DeclaredSystemInstance instance, ImmutableContentRef frozen) => ApplicationInventoryOperations.Of(
                instance.Retire(provenance, frozen, request.ApplicationId, request.ExpectedRevision,
                    request.EffectiveAt, request.Reason, memberId, display, provenance.AcceptedAt))), context, ct)
            .ConfigureAwait(false);
        return ApplicationInventoryOperations.UnitOutcome(outcome);
    }

    static Result Prepare(DeclaredSystemInstance instance, RetireSystemInstance request, Uuid memberId,
        string display, DateTimeOffset at) => ApplicationInventoryOperations.Of(instance.PrepareRetire(
        request.ApplicationId, request.ExpectedRevision, request.EffectiveAt, request.Reason, memberId, display, at));

    static RetireSystemInstance Canonical(RetireSystemInstance request) =>
        request with { Reason = request.Reason?.Trim() ?? string.Empty };

    static DeclaredSystemInstance Owner(RetireSystemInstance request) =>
        new(request.TenantId, request.SystemInstanceId, request.CommandId);
}
