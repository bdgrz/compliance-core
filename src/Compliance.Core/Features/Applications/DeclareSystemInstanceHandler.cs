using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Declares a system instance as the root native acceptance of its client-chosen identity on the instance's own
///     stream. The application's revision is a freshness floor and is read at the first acceptance only; an exact
///     retry replays the original registration after any later change to either owner.
/// </summary>
public sealed class DeclareSystemInstanceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<DeclareSystemInstance, SystemInstanceRegistration>
{
    const string CommandKind = "DeclareSystemInstance";

    public async ValueTask<Result<SystemInstanceRegistration>> HandleAsync(
        IRequestContext<DeclareSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.SystemInstanceId == Uuid.Empty || request.TenantId == Uuid.Empty ||
            request.ApplicationId == Uuid.Empty)
            return Invalid(RequestErrorKind.Validation,
                "A system instance declaration requires its instance, tenant and application identity.");
        if (request.ExpectedApplicationRevision < 1)
            return Invalid(RequestErrorKind.Validation, "The expected application revision must be positive.");
        var current = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        var provenance = ApplicationInventoryOperations.Provenance(request.SystemInstanceId, CommandKind,
            current.Stream, memberId, null, AuthoredIntentDigest.Compute(Canonical(request)), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var application = await reader.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct)
                .ConfigureAwait(false);
            if (!application.IsCreated)
                return Invalid(RequestErrorKind.NotFound, "The application was not found.");
            if (application.IsRetired)
                return Invalid(RequestErrorKind.Conflict, "A retired application cannot receive a new system instance.");
            // V1 retains the field name. It is now a source freshness floor, not a lock on
            // unrelated application metadata edits.
            if (application.Revision < request.ExpectedApplicationRevision)
                return Result<SystemInstanceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The application source has not reached revision {request.ExpectedApplicationRevision}.",
                    isTransient: true));
            response = await ApplicationInventoryOperations.RegistrationAsync(content, request.TenantId,
                ApplicationInventoryOperations.SystemInstanceRegistrationSchema,
                new SystemInstanceRegistration(request.SystemInstanceId), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<DeclaredSystemInstance>(binding,
            () => Owner(request), instance => instance.Acceptance,
            instance => ApplicationInventoryOperations.Prepared(instance.PrepareDeclare(request.ApplicationId,
                request.Name, request.Kind, request.AccessBoundaryReference, request.SourceIdentifier), response),
            (DeclaredSystemInstance instance, ImmutableContentRef frozen) => instance.Declare(provenance, frozen,
                request.ApplicationId, request.Name, request.Kind, request.AccessBoundaryReference,
                request.SourceIdentifier, memberId, display, provenance.AcceptedAt)), context, ct)
            .ConfigureAwait(false);
        return await ApplicationImportContent.ResponseAsync<SystemInstanceRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static DeclareSystemInstance Canonical(DeclareSystemInstance request) => request with
    {
        Name = request.Name?.Trim()!,
        Kind = request.Kind?.Trim()!,
        AccessBoundaryReference = ApplicationInventoryOperations.Optional(request.AccessBoundaryReference),
        SourceIdentifier = ApplicationInventoryOperations.Optional(request.SourceIdentifier),
    };

    static DeclaredSystemInstance Owner(DeclareSystemInstance request) =>
        new(request.TenantId, request.SystemInstanceId, request.SystemInstanceId);

    static Result<SystemInstanceRegistration> Invalid(RequestErrorKind kind, string message) =>
        Result<SystemInstanceRegistration>.Failure(new RequestError(kind, message));
}
