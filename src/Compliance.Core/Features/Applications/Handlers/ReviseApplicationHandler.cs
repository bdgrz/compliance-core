using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Revises an application's metadata as one native operation on its stream. The authored intent is the canonical
///     <see cref="ApplicationUpdateAuthoredIntent" />, so keeping and clearing an owner field never share a command
///     id. An exact retry replays the original result after any later change.
/// </summary>
public sealed class ReviseApplicationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, ApplicationGovernedRevisionGate? gate = null)
    : IRequestHandler<ReviseApplication>
{
    const string CommandKind = "ReviseApplication";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ApplicationId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An application revision requires a command id, tenant and application."));
        if (request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected application revision must not be negative."));
        var governed = gate?.IsEnabled ?? false;
        var current = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        var provenance = ApplicationInventoryOperations.Provenance(request.CommandId, CommandKind, current.Stream,
            memberId, request.ExpectedRevision,
            AuthoredIntentDigest.Compute(ApplicationUpdateAuthoredIntent.From(request)), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ApplicationRevisionContent? validated = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var invalid = Resolve(current, request, governed, out var resolved);
            if (invalid is not null)
                return Result.Failure(invalid.ToRequestError());
            var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
                resolved.SystemOwnerPersonId, resolved.AccessOwnerPersonId, ct).ConfigureAwait(false);
            if (owners is not null)
                return Result.Failure(owners);
            validated = resolved;
            response = await ApplicationInventoryOperations.UnitAsync(content, request.TenantId, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<DeclaredApplication>(binding,
            () => Owner(request), app => app.Acceptance,
            app => ApplicationInventoryOperations.Prepared(Prepare(app, request, governed, validated), response),
            (DeclaredApplication app, ImmutableContentRef frozen) => ApplicationInventoryOperations.Of(app.Revise(
                provenance, frozen, request.ExpectedRevision, request.Name, request.Purpose, request.OwnerReference,
                memberId, display, provenance.AcceptedAt, request.Classification, request.SystemOwnerPersonId,
                request.AccessOwnerPersonId, request.IsRestricted, request.ApplicationKind, request.Criticality,
                governed, request.ClassificationIntent, request.SystemOwnerIntent, request.AccessOwnerIntent))),
            context, ct).ConfigureAwait(false);
        return ApplicationInventoryOperations.UnitOutcome(outcome);
    }

    static Result Prepare(DeclaredApplication app, ReviseApplication request, bool governed,
        ApplicationRevisionContent? validated)
    {
        var changed = Resolve(app, request, governed, out var resolved);
        if (changed is not null)
            return Result.Failure(changed.ToRequestError());
        // The owners were validated against the content resolved from the hydration this decision started from.
        return validated is not null && resolved != validated
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application content changed after owner validation."))
            : Result.Success;
    }

    static CommandFailure? Resolve(DeclaredApplication app, ReviseApplication request,
        bool governed, out ApplicationRevisionContent content) =>
        app.ResolveRevisionContent(request.ExpectedRevision, request.Name, request.Purpose,
            request.OwnerReference, request.Classification, request.SystemOwnerPersonId,
            request.AccessOwnerPersonId, request.IsRestricted, request.ApplicationKind,
            request.Criticality, governed, request.ClassificationIntent,
            request.SystemOwnerIntent, request.AccessOwnerIntent, out content);

    static DeclaredApplication Owner(ReviseApplication request) =>
        new(request.TenantId, request.ApplicationId, request.CommandId);
}
