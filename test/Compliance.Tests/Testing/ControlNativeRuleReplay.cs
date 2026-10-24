using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

static class ControlNativeRuleReplay
{
    public static async Task<ControlRules> ReadAsync(ControlRules prepared)
    {
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        await ControlSeeds.WriteAsync(scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), content, prepared);
        var facts = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(prepared.TenantId.ToString(),
            ControlDraft.Area, prepared.Id.ToString()), EventCursor.Start, CancellationToken.None))
        {
            var clone = (DomainEvent)JsonSerializer.Deserialize(JsonSerializer.Serialize(record.Event,
                record.Event.GetType(), ComplianceCoreJsonContext.Default), record.Event.GetType(),
                ComplianceCoreJsonContext.Default)!;
            facts.Add(clone);
        }
        var compact = new AggregateScenario<ControlDraft>(new ControlDraft(prepared.TenantId, prepared.Id))
            .Given(facts.ToArray()).Aggregate;
        Assert.True(compact.IsCreated);
        Assert.NotNull(compact.CurrentAuthority);
        return (await new ControlReader(reader, events, content).ReadAsync(compact, prepared.ProgramId,
            CancellationToken.None)).Rules;
    }
}
