using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Readers;

public sealed class ProductClientServiceReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async Task<ProductClientServiceHistorySnapshot> HistoryAsync(Uuid tenantId, Uuid serviceId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct).ConfigureAwait(false);
        return await ProductClientServiceHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
    }

    public async Task<ProductClientServiceRules> RulesAsync(Uuid tenantId, Uuid serviceId, CancellationToken ct = default)
    {
        var history = await HistoryAsync(tenantId, serviceId, ct).ConfigureAwait(false);
        var rules = new ProductClientServiceRules(tenantId, serviceId);
        foreach (var fact in history.Facts.OfType<IProductClientServiceFact>())
        {
            var member = Uuid.Parse(fact.Operation.Actor.Id, CultureInfo.InvariantCulture);
            var actor = fact.Operation.Actor.Display;
            if (fact is ClientServiceRetired retirement)
            {
                var value = await ReadRetirementAsync(content, retirement, ct).ConfigureAwait(false);
                if (rules.Retire(fact.Revision - 1, value.Rationale, member, actor, fact.ChangedAt) is not null)
                    throw new ImmutableContentIntegrityException("The service retirement differs from its recorded business transition.");
            }
            else
            {
                var value = await ReadContentAsync(content, fact, ct).ConfigureAwait(false);
                if (fact is ClientServiceCreated)
                {
                    var created = rules.Create(fact.ProgramId, value.Name, value.Purpose, value.OwnerReference, member, actor, fact.ChangedAt);
                    if (!created.IsSuccess)
                        throw new ImmutableContentIntegrityException("The service creation differs from its recorded business transition.");
                }
                else if (rules.Revise(fact.Revision - 1, value.Name, value.Purpose, value.OwnerReference, member, actor, fact.ChangedAt) is not null)
                    throw new ImmutableContentIntegrityException("The service revision differs from its recorded business transition.");
            }
        }
        rules.ClearPendingChanges();
        return rules;
    }

    public static async Task<ProductClientServiceContent> ReadContentAsync(IImmutableContentStore content,
        IProductClientServiceFact fact, CancellationToken ct = default)
    {
        ProductClientServiceHistory.ValidateHeader(fact);
        var value = await content.ReadJsonAsync<ProductClientServiceContent>(fact.TenantId.ToString(), fact.Content, ct).ConfigureAwait(false);
        if (ProductClientServiceRules.Validate(value) is not null || value.Name != value.Name.Trim() ||
            value.Purpose != value.Purpose.Trim() || value.OwnerReference != value.OwnerReference.Trim())
            throw new ImmutableContentIntegrityException("The service value is malformed or not canonical.");
        var digest = fact switch
        {
            ClientServiceCreated => AuthoredIntentDigest.Compute(new CreateClientService(fact.ServiceId, fact.TenantId, fact.ProgramId,
                value.Name, value.Purpose, value.OwnerReference)),
            ClientServiceRevised => AuthoredIntentDigest.Compute(new ReviseClientService(fact.Operation.LogicalId, fact.TenantId, fact.ServiceId,
                (long)fact.Operation.OriginalExpectedRevision!.Value, value.Name, value.Purpose, value.OwnerReference)),
            ClientServiceRetired => null,
            _ => throw new ImmutableContentIntegrityException("The service has another value-producing fact kind."),
        };
        if (digest is not null && digest != fact.Operation.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The service value differs from its exact authored command binding.");
        return value;
    }

    public static async Task<ProductClientServiceRetirement> ReadRetirementAsync(IImmutableContentStore content,
        ClientServiceRetired fact, CancellationToken ct = default)
    {
        ProductClientServiceHistory.ValidateHeader(fact);
        var value = await content.ReadJsonAsync<ProductClientServiceRetirement>(fact.TenantId.ToString(), fact.Retirement, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value.Rationale) || value.Rationale != value.Rationale.Trim() ||
            AuthoredIntentDigest.Compute(new RetireClientService(fact.Operation.LogicalId, fact.TenantId, fact.ServiceId,
                (long)fact.Operation.OriginalExpectedRevision!.Value, value.Rationale)) != fact.Operation.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The service retirement differs from its exact authored command binding.");
        return value;
    }
}
