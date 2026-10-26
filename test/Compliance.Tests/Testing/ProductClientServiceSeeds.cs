using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Testing;

static class ProductClientServiceSeeds
{
    public static Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProductClientServiceRules rules) => SaveAsync(reader, writer, events, content, rules, false);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProductClientServiceRules rules) => SaveAsync(reader, writer, events, content, rules, true);

    static async Task SaveAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, ProductClientServiceRules rules, bool append)
    {
        foreach (var change in (append ? rules.PendingChanges : rules.AppliedChanges).ToArray())
        {
            var logical = change.Kind == nameof(CreateClientService) ? rules.Id : Uuid.CreateVersion4();
            var owner = await reader.HydrateAsync(new ClientService(rules.TenantId, rules.Id, logical));
            _ = await ProductClientServiceHistory.ReadAsync(events, owner);
            var fact = await FreezeAsync(content, rules.TenantId, rules.Id, change, owner.NativeSequence + 1, owner.CurrentContent, logical);
            var outcome = fact switch
            {
                ClientServiceCreated created => owner.Create(created.Operation, created.ProgramId, created.Content,
                    created.FirstResponse!, created.OperationResult!),
                ClientServiceRevised revised => owner.Revise(revised.Operation, revised.Content, revised.FirstResponse!, revised.OperationResult!),
                ClientServiceRetired retired => owner.Retire(retired.Operation, retired.Retirement, retired.FirstResponse!, retired.OperationResult!),
                _ => throw new InvalidOperationException("Another client service fixture fact."),
            };
            Assert.True(outcome.IsSuccess, outcome.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
        rules.ClearPendingChanges();
    }

    public static async Task<IReadOnlyList<DomainEvent>> FactsAsync(IImmutableContentStore content, ProductClientServiceRules rules)
    {
        var facts = new List<DomainEvent>();
        ImmutableContentRef? current = null;
        foreach (var change in rules.AppliedChanges)
        {
            var fact = await FreezeAsync(content, rules.TenantId, rules.Id, change, (ulong)facts.Count + 1, current);
            current = ((IProductClientServiceFact)fact).Content;
            facts.Add(fact);
        }
        return facts;
    }

    public static async Task<ClientService> OwnerAsync(IImmutableContentStore content, ProductClientServiceRules rules)
    {
        var owner = new ClientService(rules.TenantId, rules.Id);
        new AggregateScenario<ClientService>(owner).Given((await FactsAsync(content, rules)).ToArray());
        return owner;
    }

    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, Uuid tenant, Uuid service,
        ProductClientServiceChange change, ulong sequence, ImmutableContentRef? current = null, Uuid logicalId = default)
    {
        var logical = logicalId != Uuid.Empty ? logicalId : change.Kind == nameof(CreateClientService) ? service : Uuid.CreateVersion4();
        var digest = change.Kind switch
        {
            nameof(CreateClientService) => AuthoredIntentDigest.Compute(new CreateClientService(service, tenant, change.ProgramId,
                change.Content!.Name, change.Content.Purpose, change.Content.OwnerReference)),
            nameof(ReviseClientService) => AuthoredIntentDigest.Compute(new ReviseClientService(logical, tenant, service,
                change.Revision - 1, change.Content!.Name, change.Content.Purpose, change.Content.OwnerReference)),
            nameof(RetireClientService) => AuthoredIntentDigest.Compute(new RetireClientService(logical, tenant, service, change.Revision - 1, change.Rationale!)),
            _ => throw new InvalidOperationException("Another client service fixture change."),
        };
        var owner = new ClientService(tenant, service);
        var operation = OwnerProvenance.For(owner, logical, change.Kind, change.Actor, RootOperation.InitialEpisode,
            change.Kind == nameof(CreateClientService) ? null : unchecked((ulong)(change.Revision - 1)), digest, change.ChangedAt)
            with
        { FirstOwner = owner.Stream };
        var value = change.Content is { } authored
            ? await NativeContent.WriteAsync(content, tenant, ProductClientServiceContent.Schema, authored) : current!;
        var response = change.Kind == nameof(CreateClientService)
            ? await NativeContent.FreezeAsync(content, tenant, ProductClientServiceOperations.RegistrationSchema, new ClientServiceRegistration(service))
            : await NativeContent.FreezeUnitAsync(content, tenant);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response.Response);
        return change.Kind switch
        {
            nameof(CreateClientService) => new ClientServiceCreated(tenant, service, change.ProgramId, change.Revision, value,
                change.ChangedAt, operation, first, response.Result, sequence),
            nameof(ReviseClientService) => new ClientServiceRevised(tenant, service, change.ProgramId, change.Revision, value,
                change.ChangedAt, operation, first, response.Result, sequence),
            _ => new ClientServiceRetired(tenant, service, change.ProgramId, change.Revision, value,
                await NativeContent.WriteAsync(content, tenant, ProductClientServiceRetirement.Schema,
                    new ProductClientServiceRetirement(change.Rationale!)), change.ChangedAt, operation, first, response.Result, sequence),
        };
    }
}
