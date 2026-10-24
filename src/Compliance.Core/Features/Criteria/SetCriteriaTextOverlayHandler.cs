using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
///     Sets or revises one criterion's licensed text overlay at its own revision. The complete supplied content is
///     written as immutable content before one revision reference is appended, and the registration is frozen as the
///     first response, so an exact retry returns the original revision even after later edits and a changed intent
///     under the same command id conflicts.
/// </summary>
public sealed class SetCriteriaTextOverlayHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ICriteriaCatalog catalog, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>
{
    const string CommandKind = "SetCriteriaTextOverlay";

    public async ValueTask<Result<CriteriaTextOverlayRegistration>> HandleAsync(
        IRequestContext<SetCriteriaTextOverlay> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "An overlay requires a command id and a non-negative expected revision.");
        if (catalog.GetEntry(request.EditionId, request.Identifier) is not { } entry)
            return Failure(RequestErrorKind.NotFound, "The criterion was not found.");

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new CriteriaTextOverlay(request.TenantId, request.EditionId, entry.Identifier).Stream;
        var provenance = RootOperation.Provenance(request.CommandId, CommandKind, stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new CriteriaTextOverlay(request.TenantId, request.EditionId,
            entry.Identifier, request.CommandId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? overlay = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (CriteriaTextOverlayRules.Clean(request.Content) is not { } cleaned)
                return Failure(RequestErrorKind.Validation,
                    "An overlay requires bounded supplied text, supplier, license and member attribution.");
            var prepared = current.PrepareSet(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<CriteriaTextOverlayRegistration>.Failure(prepared.Error!);
            overlay = await NativeContent.WriteAsync(content, request.TenantId, CriteriaTextOverlay.ContentSchema,
                cleaned, ct).ConfigureAwait(false);
            (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                CriteriaTextOverlay.RegistrationSchema, new CriteriaTextOverlayRegistration(request.EditionId,
                    request.Identifier, request.ExpectedRevision + 1), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CriteriaTextOverlay>(binding,
            () => new CriteriaTextOverlay(request.TenantId, request.EditionId, entry.Identifier, request.CommandId),
            overlayLine => overlayLine.Acceptance, overlayLine => Prepare(overlayLine, request.ExpectedRevision, response),
            (CriteriaTextOverlay overlayLine, ImmutableContentRef frozen) => overlayLine.Set(provenance, overlay!,
                frozen, result!)), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<CriteriaTextOverlayRegistration>(content, request.TenantId, outcome,
            ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(CriteriaTextOverlay overlay, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = overlay.PrepareSet(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The overlay changed while the revision was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<CriteriaTextOverlayRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CriteriaTextOverlayRegistration>.Failure(new RequestError(kind, message));
}
