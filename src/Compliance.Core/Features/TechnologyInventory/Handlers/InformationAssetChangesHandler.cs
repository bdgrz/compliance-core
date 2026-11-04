using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>Invokes the asset owner for its four named changes; Portia owns execution and exact retries.</summary>
public sealed class InformationAssetChangesHandler(NativeOperationExecutor operations,
    TechnologyInventoryReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ClassifyInformationAsset>, IRequestHandler<ChangeInformationAssetDetails>,
      IRequestHandler<ChangeInformationAssetOwner>, IRequestHandler<RetireInformationAsset>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ClassifyInformationAsset> context, CancellationToken ct) =>
        ExecuteAsync(context, ct);

    public ValueTask<Result> HandleAsync(IRequestContext<ChangeInformationAssetDetails> context, CancellationToken ct) =>
        ExecuteAsync(context, ct);

    public ValueTask<Result> HandleAsync(IRequestContext<ChangeInformationAssetOwner> context, CancellationToken ct) =>
        ExecuteAsync(context, ct);

    public ValueTask<Result> HandleAsync(IRequestContext<RetireInformationAsset> context, CancellationToken ct) =>
        ExecuteAsync(context, ct);

    async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, CancellationToken ct)
        where T : IRequest, IInformationAssetChange
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty ||
            request.InformationAssetId == Uuid.Empty || request.ExpectedRevision < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An asset change requires command, tenant and asset identities and a positive expected revision."));
        var actor = TechnologyInventoryActor.From(context);
        var at = clock.GetUtcNow();
        var provenance = InventoryOperations.Provenance(new InformationAsset(request.TenantId, request.InformationAssetId).Stream,
            request.CommandId, request.GetType().Name, actor, request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), at);
        var binding = AcceptedOperationBinding.Of(provenance);
        var response = await InventoryOperations.FreezeAsync(content, request.TenantId.ToString(), UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var outcome = await operations.ExecuteAsync(new NativeOperation<InformationAsset>(binding,
            () => new InformationAsset(request.TenantId, request.InformationAssetId, request.CommandId),
            asset => asset.Acceptance,
            async (asset, prepareCt) =>
            {
                if (request is ChangeInformationAssetOwner owner &&
                    await references.ValidateOwnerAsync(request.TenantId, owner.OwnerPersonId, prepareCt)
                        .ConfigureAwait(false) is { } ownerFailure)
                    return Result<FirstResponseDescriptor>.Failure(ownerFailure);
                if (request is RetireInformationAsset retirement &&
                    (string.IsNullOrWhiteSpace(retirement.Reason) || retirement.Reason.Trim().Length > 2000))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                        "Retiring an information asset requires a reason of at most 2000 characters."));
                var prepared = asset.PrepareNamedChange(request.ExpectedRevision, value => request switch
                {
                    ClassifyInformationAsset classified => value with { Classification = classified.Classification },
                    ChangeInformationAssetDetails details => value with
                    {
                        Name = details.Name, RetentionReference = details.RetentionReference, Description = details.Description
                    },
                    ChangeInformationAssetOwner changed => value with { OwnerPersonId = changed.OwnerPersonId },
                    RetireInformationAsset => value with { Lifecycle = "retired" },
                    _ => throw new InvalidOperationException("Unknown information asset change.")
                });
                return prepared.IsSuccess ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (InformationAsset asset, ImmutableContentRef frozen) => request switch
            {
                ClassifyInformationAsset classified => asset.Classify(provenance, frozen, request.ExpectedRevision,
                    classified.Classification, actor, at),
                ChangeInformationAssetDetails details => asset.ChangeDetails(provenance, frozen, request.ExpectedRevision,
                    details.Name, details.RetentionReference, details.Description, actor, at),
                ChangeInformationAssetOwner owner => asset.ChangeOwner(provenance, frozen, request.ExpectedRevision,
                    owner.OwnerPersonId, actor, at),
                RetireInformationAsset retirement => asset.Retire(provenance, frozen, request.ExpectedRevision,
                    retirement.Reason, retirement.EffectiveAt ?? at, actor, at),
                _ => throw new InvalidOperationException("Unknown information asset change.")
            }), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadUnitAsync(content, request.TenantId.ToString(), outcome, ct).ConfigureAwait(false);
    }
}
