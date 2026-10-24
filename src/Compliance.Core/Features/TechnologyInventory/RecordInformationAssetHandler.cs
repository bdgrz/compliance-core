using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Records a information asset as the native fact of its own logical operation. The identity in the command is the
///     logical identity, so an exact retry replays the original answer after any later revision, and the same identity
///     with a different actor, kind or intent conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class RecordInformationAssetHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<RecordInformationAsset, InformationAssetRegistration>
{
    const string CommandKind = "RecordInformationAsset";

    public async ValueTask<Result<InformationAssetRegistration>> HandleAsync(IRequestContext<RecordInformationAsset> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.InformationAssetId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<InformationAssetRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A information asset identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new InformationAsset(request.TenantId, request.InformationAssetId).Stream,
            request.InformationAssetId, CommandKind, actor, null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new InformationAsset(request.TenantId, request.InformationAssetId, request.InformationAssetId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        var content = new InformationAssetContent(request.Name, request.Classification,
            request.RetentionReference, request.OwnerPersonId, request.Description,
            TechnologyInventoryRules.Active);

        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return Result<InformationAssetRegistration>.Failure(error);
        }
        var realm = request.TenantId.ToString();
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, "information-asset-registration.v1",
            new InformationAssetRegistration(request.InformationAssetId), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<InformationAsset>(binding,
            () => new InformationAsset(request.TenantId, request.InformationAssetId, request.InformationAssetId), asset => asset.Acceptance,
            asset =>
            {
                var prepared = asset.PrepareRecord(content);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (InformationAsset asset, ImmutableContentRef frozen) => asset.Record(provenance, frozen, content, actor,
                provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadAsync<InformationAssetRegistration>(contentStore, realm, "information-asset-registration.v1", accepted, ct)
            .ConfigureAwait(false);
    }
}
