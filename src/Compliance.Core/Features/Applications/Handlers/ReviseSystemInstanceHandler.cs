using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Revises a system instance as one native operation on the instance's stream. A revision that would leave the
///     recorded content unchanged records nothing, exactly as before; every other revision is accepted under its
///     command id and an exact retry replays the original result after any later change.
/// </summary>
public sealed class ReviseSystemInstanceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviseSystemInstance>
{
    const string CommandKind = "ReviseSystemInstance";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty ||
            request.ApplicationId == Uuid.Empty || request.SystemInstanceId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A system instance revision requires a command id, tenant, application and instance."));
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
            if (current.ApplicationId == request.ApplicationId &&
                current.IsUnchanged(request.Name, request.SourceIdentifier, request.AccessBoundary))
                return Result.Success;
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
                instance.Revise(provenance, frozen, request.ApplicationId, request.ExpectedRevision, request.Name,
                    request.SourceIdentifier, request.AccessBoundary, request.Rationale, memberId, display,
                    provenance.AcceptedAt))), context, ct).ConfigureAwait(false);
        return ApplicationInventoryOperations.UnitOutcome(outcome);
    }

    static Result Prepare(DeclaredSystemInstance instance, ReviseSystemInstance request, Uuid memberId,
        string display, DateTimeOffset at) => ApplicationInventoryOperations.Of(instance.PrepareRevise(
        request.ApplicationId, request.ExpectedRevision, request.Name, request.SourceIdentifier,
        request.AccessBoundary, request.Rationale, memberId, display, at));

    static ReviseSystemInstance Canonical(ReviseSystemInstance request) => request with
    {
        Name = request.Name?.Trim()!,
        SourceIdentifier = ApplicationInventoryOperations.Optional(request.SourceIdentifier) ?? string.Empty,
        AccessBoundary = ApplicationInventoryOperations.Optional(request.AccessBoundary) ?? string.Empty,
        Rationale = request.Rationale?.Trim()!,
    };

    static DeclaredSystemInstance Owner(ReviseSystemInstance request) =>
        new(request.TenantId, request.SystemInstanceId, request.CommandId);
}
