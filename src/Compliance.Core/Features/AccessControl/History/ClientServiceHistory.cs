using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.History;

/// <summary>Reads complete original service bodies from the exact captured native prefix.</summary>
static class ClientServiceHistory
{
    public static async Task<IReadOnlyList<NonattestServiceView>> ReadAsync(IDomainEventReader events,
        IImmutableContentStore content, ClientServiceIndependence owner, CancellationToken ct)
    {
        var history = await ClientServiceNativeHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var services = new List<NonattestServiceView>(history.Services.Count);
        foreach (var fact in history.Services)
        {
            if (fact.ServiceContent.Schema != IndependenceContent.ServiceSchema)
                throw new ImmutableContentIntegrityException("The service body has another selected schema.");
            var view = await content.ReadJsonAsync<NonattestServiceView>(owner.Stream.Realm, fact.ServiceContent, ct).ConfigureAwait(false);
            if (view.ClientTenantId != owner.TenantId || view.ServiceRecordId != fact.OperationId ||
                view.Actor != fact.Operation.Actor || view.RecordedAt != fact.Operation.AcceptedAt ||
                !IndependenceRecordValidation.ValidService(view.Content) ||
                AuthoredIntentDigest.Compute(new RecordNonattestService(owner.TenantId, fact.OperationId,
                    (long)fact.Operation.OriginalExpectedRevision!.Value, view.Content)) != fact.Operation.AuthoredIntentSha256)
                throw new ImmutableContentIntegrityException("The service body differs from its exact original native request and attribution.");
            var result = await content.ReadJsonAsync<MutationResultContent>(owner.Stream.Realm, fact.OperationResult!, ct).ConfigureAwait(false);
            if (result.Schema != IndependenceContent.ServiceSchema || result.Value is not StandardResponseBody body || body.Content != fact.ServiceContent)
                throw new ImmutableContentIntegrityException("The service result does not freeze its exact original typed body.");
            services.Add(view);
        }
        return Array.AsReadOnly(services.ToArray());
    }
}
