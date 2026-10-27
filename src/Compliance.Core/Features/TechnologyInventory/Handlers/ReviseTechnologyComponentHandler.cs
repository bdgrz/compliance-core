using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Revises a component as the native fact of its own logical operation. <c>CommandId</c> is the logical identity,
///     so an exact retry replays the original answer after any later revision, and the same identity with a different
///     actor, intent or expected revision conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class ReviseTechnologyComponentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<ReviseTechnologyComponent>
{
    const string CommandKind = "ReviseTechnologyComponent";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseTechnologyComponent> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ComponentId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, component identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new TechnologyComponent(request.TenantId, request.ComponentId).Stream,
            request.CommandId, CommandKind, actor, request.ExpectedRevision, AuthoredIntentDigest.Compute(request),
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new TechnologyComponent(request.TenantId, request.ComponentId, request.CommandId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return Result.Failure(error);
        }

        var realm = request.TenantId.ToString();
        Func<TechnologyComponentContent, TechnologyComponentContent> change = current => current with
        {
            Name = request.Name,
            OwnerPersonId = request.OwnerPersonId,
            Lifecycle = request.Lifecycle,
            EnvironmentReference = request.EnvironmentReference,
            LocationReference = request.LocationReference,
            EndpointCount = request.EndpointCount,
            ManagementSource = request.ManagementSource,
        };
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TechnologyComponent>(binding,
            () => new TechnologyComponent(request.TenantId, request.ComponentId, request.CommandId), component => component.Acceptance,
            component =>
            {
                var prepared = component.PrepareRevise(request.ExpectedRevision, change);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (TechnologyComponent component, ImmutableContentRef frozen) => component.Revise(provenance, frozen,
                request.ExpectedRevision, change, actor, provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadUnitAsync(contentStore, realm, accepted, ct).ConfigureAwait(false);
    }
}
