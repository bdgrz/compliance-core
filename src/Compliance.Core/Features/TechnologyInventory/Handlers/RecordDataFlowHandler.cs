using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Records a data flow as the native fact of its own logical operation. The identity in the command is the
///     logical identity, so an exact retry replays the original answer after any later revision, and the same identity
///     with a different actor, kind or intent conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class RecordDataFlowHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<RecordDataFlow, DataFlowRegistration>
{
    const string CommandKind = "RecordDataFlow";

    public async ValueTask<Result<DataFlowRegistration>> HandleAsync(IRequestContext<RecordDataFlow> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.DataFlowId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<DataFlowRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A data flow identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new DataFlow(request.TenantId, request.DataFlowId).Stream,
            request.DataFlowId, CommandKind, actor, null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new DataFlow(request.TenantId, request.DataFlowId, request.DataFlowId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        var classification = Result<string>.Success(string.Empty);

        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return Result<DataFlowRegistration>.Failure(error);
            classification = await references.ResolveFlowAsync(request.TenantId, request.SourceType,
                request.SourceId, request.DestinationType, request.DestinationId, request.InformationAssetIds, ct)
                .ConfigureAwait(false);
            if (!classification.IsSuccess)
                return Result<DataFlowRegistration>.Failure(classification.Error!);
        }

        var content = new DataFlowContent(request.SourceType, request.SourceId,
            request.DestinationType, request.DestinationId, request.DestinationParty,
            request.InformationAssetIds, request.Purpose, request.EncryptedInTransit,
            request.EncryptedAtRest, request.ExceptionReference, request.EffectiveFrom,
            request.OwnerPersonId, TechnologyInventoryRules.Active, classification.Value ?? string.Empty);
        var realm = request.TenantId.ToString();
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, "data-flow-registration.v1",
            new DataFlowRegistration(request.DataFlowId), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<DataFlow>(binding,
            () => new DataFlow(request.TenantId, request.DataFlowId, request.DataFlowId), flow => flow.Acceptance,
            flow =>
            {
                var prepared = flow.PrepareRecord(content);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (DataFlow flow, ImmutableContentRef frozen) => flow.Record(provenance, frozen, content, actor,
                provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadAsync<DataFlowRegistration>(contentStore, realm, "data-flow-registration.v1", accepted, ct)
            .ConfigureAwait(false);
    }
}
