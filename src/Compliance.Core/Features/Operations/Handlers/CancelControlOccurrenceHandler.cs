using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Checks current source membership and Lead permission before resolving any accepted cancellation.</summary>
public sealed class CancelControlOccurrenceHandler(NativeOperationExecutor operations,
    IAggregateReader reader, ControlOperationsReader occurrences, IPermissionAuthorizer permissions,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelControlOccurrence, ControlOccurrenceView>
{
    public ValueTask<Result<ControlOccurrenceView>> HandleAsync(IRequestContext<CancelControlOccurrence> context,
        CancellationToken ct) => ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOccurrenceView>> HandleCoreAsync(IRequestContext<CancelControlOccurrence> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Failure(RequestErrorKind.Forbidden, "Only a current Compliance Lead may cancel an occurrence.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ControlId == Uuid.Empty || request.OccurrenceId == Uuid.Empty || request.ExpectedRevision < 0 ||
            !ControlOperationRules.IsBoundedText(request.Reason))
            return Failure(RequestErrorKind.Validation, "Cancellation requires its exact scope, revision and bounded reason.");
        var now = clock.GetUtcNow();
        var memberId = RbacIds.Member(request.TenantId, userId);
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, memberId), ct).ConfigureAwait(false);
        if (!member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) || member.UserId != userId ||
            member.Affiliation == "firm_staff" || !await permissions.IsAllowedAsync(request.TenantId, userId,
                memberId, ControlOccurrencePermissions.Cancel, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden, "Only a current Compliance Lead may cancel an occurrence.");
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var stream = new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId, request.OccurrenceId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, nameof(CancelControlOccurrence),
            stream, actor, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(
            AcceptedOperationBinding.Of(provenance),
            () => new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId,
                request.OccurrenceId, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var (source, view) = await occurrences.ReadStateAsync(request.TenantId, request.ProgramId,
                    request.ControlId, request.OccurrenceId, Uuid.Empty, token).ConfigureAwait(false);
                if (view is null)
                    return Result<FirstResponseDescriptor>.Failure(ControlOperationsSource.OccurrenceNotFound());
                if (source.Revision != owner.Revision)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The occurrence changed while cancellation was prepared.", true));
                var guard = owner.PrepareProof(request.ExpectedRevision, false);
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                if (view.State is not ("open" or "returned"))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "Only an open occurrence may be cancelled."));
                var frozen = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                    ControlOperatingContent.OccurrenceViewSchema,
                    view with { Revision = owner.Revision + 1, State = "cancelled" }, token).ConfigureAwait(false);
                result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                    ControlOperatingContent.OccurrenceViewSchema, frozen, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(
                    ControlOperatingContent.OkResponse(ControlOperatingContent.OccurrenceViewSchema, frozen));
            }, (owner, frozen) => owner.Cancel(provenance, request.Reason, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<ControlOccurrenceView> Failure(RequestErrorKind kind, string message) =>
        Result<ControlOccurrenceView>.Failure(new RequestError(kind, message));
}
