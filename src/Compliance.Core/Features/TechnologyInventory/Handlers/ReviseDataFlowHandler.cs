using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Revises a data flow as the native fact of its own logical operation. <c>CommandId</c> is the logical identity,
///     so an exact retry replays the original answer after any later revision, and the same identity with a different
///     actor, intent or expected revision conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class ReviseDataFlowHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<ReviseDataFlow>
{
    const string CommandKind = "ReviseDataFlow";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseDataFlow> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.DataFlowId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, data flow identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new DataFlow(request.TenantId, request.DataFlowId).Stream,
            request.CommandId, CommandKind, actor, request.ExpectedRevision, AuthoredIntentDigest.Compute(request),
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new DataFlow(request.TenantId, request.DataFlowId, request.CommandId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        var classification = Result<string>.Success(string.Empty);
        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return Result.Failure(error);
            classification = await references.ResolveFlowAsync(request.TenantId, request.SourceType,
                request.SourceId, request.DestinationType, request.DestinationId, request.InformationAssetIds, ct)
                .ConfigureAwait(false);
            if (!classification.IsSuccess)
                return Result.Failure(classification.Error!);
        }

        var realm = request.TenantId.ToString();
        Func<DataFlowContent, DataFlowContent> change = _ => new DataFlowContent(request.SourceType, request.SourceId,
                request.DestinationType, request.DestinationId, request.DestinationParty,
                request.InformationAssetIds, request.Purpose, request.EncryptedInTransit,
                request.EncryptedAtRest, request.ExceptionReference, request.EffectiveFrom,
                request.OwnerPersonId, request.Lifecycle, classification.Value ?? string.Empty);
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<DataFlow>(binding,
            () => new DataFlow(request.TenantId, request.DataFlowId, request.CommandId), flow => flow.Acceptance,
            flow =>
            {
                var prepared = flow.PrepareRevise(request.ExpectedRevision, change);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (DataFlow flow, ImmutableContentRef frozen) => flow.Revise(provenance, frozen,
                request.ExpectedRevision, change, actor, provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadUnitAsync(contentStore, realm, accepted, ct).ConfigureAwait(false);
    }
}
