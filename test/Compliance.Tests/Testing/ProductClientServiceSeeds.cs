using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
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
