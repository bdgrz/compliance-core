using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Revises a information asset as the native fact of its own logical operation. <c>CommandId</c> is the logical identity,
///     so an exact retry replays the original answer after any later revision, and the same identity with a different
///     actor, intent or expected revision conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class ReviseInformationAssetHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<ReviseInformationAsset>
{
    const string CommandKind = "ReviseInformationAsset";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseInformationAsset> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.InformationAssetId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, information asset identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new InformationAsset(request.TenantId, request.InformationAssetId).Stream,
            request.CommandId, CommandKind, actor, request.ExpectedRevision, AuthoredIntentDigest.Compute(request),
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new InformationAsset(request.TenantId, request.InformationAssetId, request.CommandId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return Result.Failure(error);
        }

        var realm = request.TenantId.ToString();
        Func<InformationAssetContent, InformationAssetContent> change = current => current with
        {
            Name = request.Name,
            Classification = request.Classification,
            RetentionReference = request.RetentionReference,
            OwnerPersonId = request.OwnerPersonId,
            Description = request.Description,
            Lifecycle = request.Lifecycle,
        };
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<InformationAsset>(binding,
            () => new InformationAsset(request.TenantId, request.InformationAssetId, request.CommandId), asset => asset.Acceptance,
            asset =>
            {
                var prepared = asset.PrepareRevise(request.ExpectedRevision, change);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (InformationAsset asset, ImmutableContentRef frozen) => asset.Revise(provenance, frozen,
                request.ExpectedRevision, change, actor, provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadUnitAsync(contentStore, realm, accepted, ct).ConfigureAwait(false);
    }
}
