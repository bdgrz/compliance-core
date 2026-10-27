using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
