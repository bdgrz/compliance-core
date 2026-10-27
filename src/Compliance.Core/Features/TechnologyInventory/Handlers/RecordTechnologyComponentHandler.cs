using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Records a component as the native fact of its own logical operation. The identity in the command is the
///     logical identity, so an exact retry replays the original answer after any later revision, and the same identity
///     with a different actor, kind or intent conflicts. References are inspected only for a new operation.
/// </summary>
public sealed class RecordTechnologyComponentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TechnologyInventoryReferences references, IImmutableContentStore contentStore, TimeProvider clock)
    : IRequestHandler<RecordTechnologyComponent, TechnologyComponentRegistration>
{
    const string CommandKind = "RecordTechnologyComponent";

    public async ValueTask<Result<TechnologyComponentRegistration>> HandleAsync(IRequestContext<RecordTechnologyComponent> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.ComponentId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<TechnologyComponentRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A component identity and tenant are required."));
        var actor = TechnologyInventoryActor.From(context);
        var provenance = InventoryOperations.Provenance(new TechnologyComponent(request.TenantId, request.ComponentId).Stream,
            request.ComponentId, CommandKind, actor, null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var decision = (await reader.HydrateAsync(new TechnologyComponent(request.TenantId, request.ComponentId, request.ComponentId), ct)
            .ConfigureAwait(false)).Acceptance.Decide(binding);
        var content = new TechnologyComponentContent(request.Category, request.Name,
            request.OwnerPersonId, request.EnvironmentReference, request.LocationReference,
            request.SystemInstanceId, request.EndpointCount, request.ManagementSource,
            TechnologyInventoryRules.Active);

        if (decision == NativeAcceptanceDecision.None)
        {
            var error = await references.ValidateOwnerAsync(request.TenantId, request.OwnerPersonId, ct)
                .ConfigureAwait(false) ??
                (request.Category?.Trim() == TechnologyInventoryRules.CloudAccount
                    ? await references.ValidateCloudAccountAsync(request.TenantId, request.SystemInstanceId, ct)
                        .ConfigureAwait(false)
                    : null);
            if (error is not null)
                return Result<TechnologyComponentRegistration>.Failure(error);
        }
        var realm = request.TenantId.ToString();
        var response = await InventoryOperations.FreezeAsync(contentStore, realm, "technology-component-registration.v1",
            new TechnologyComponentRegistration(request.ComponentId), ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TechnologyComponent>(binding,
            () => new TechnologyComponent(request.TenantId, request.ComponentId, request.ComponentId), component => component.Acceptance,
            component =>
            {
                var prepared = component.PrepareRecord(content);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (TechnologyComponent component, ImmutableContentRef frozen) => component.Record(provenance, frozen, content, actor,
                provenance.AcceptedAt)), context, ct).ConfigureAwait(false);
        return await InventoryOperations.ReadAsync<TechnologyComponentRegistration>(contentStore, realm, "technology-component-registration.v1", accepted, ct)
            .ConfigureAwait(false);
    }
}
