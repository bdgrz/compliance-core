using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class RemoveGovernedApplicationRelationshipHandler(
    IAggregateExecutor executor, IAggregateReader reader,
    RestrictedApplicationVisibility visibility, TimeProvider clock)
    : IRequestHandler<RemoveGovernedApplicationRelationship>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RemoveGovernedApplicationRelationship> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.SourceApplicationId == Uuid.Empty ||
            request.TargetApplicationId == Uuid.Empty ||
            request.SourceApplicationId == request.TargetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(request.RelationshipType) ||
            request.ExpectedRelationshipRevision < 1 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The application relationship removal is invalid."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "A personal member identity is required."));

        var source = await reader.HydrateApplicationAsync(request.TenantId,
            request.SourceApplicationId, ct).ConfigureAwait(false);
        var target = await reader.HydrateApplicationAsync(request.TenantId,
            request.TargetApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated || !target.IsCreated ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.SourceApplicationId, ct).ConfigureAwait(false) ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.TargetApplicationId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (source.IsRetired || target.IsRetired)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Relationships require active applications."));

        var targetAggregate = new DeclaredApplication(request.TenantId, request.SourceApplicationId);
        var (memberId, display) = ApplicationActor.From(context);
        return await executor.ExecuteAsync(targetAggregate,
            application => application.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result.Failure(importError))
                : CommandFailureRequestAdapter.ToOutcome(application.RemoveRelationship(
                    request.TargetApplicationId, request.ExpectedRelationshipRevision,
                    request.RelationshipType, request.Reason, memberId, display,
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
