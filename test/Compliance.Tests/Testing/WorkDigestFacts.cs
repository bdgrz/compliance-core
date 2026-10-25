using System.Text.Json;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit committed compact technical fixture facts from complete transient rule transitions.</summary>
static class WorkDigestFacts
{
    public static async Task DispatchAsync(IServiceProvider provider, Uuid tenant, Uuid member,
        Func<WorkDigestDispatchRules, bool> arrange)
    {
        var rules = new WorkDigestDispatchRules(tenant, member);
        Assert.True(arrange(rules));
        await AppendAsync(provider, new WorkDigestDispatch(tenant, member), rules.PendingEvents);
    }
    public static async Task DispatchAsync(IServiceProvider provider, WorkDigestDispatchRules rules,
        Func<WorkDigestDispatchRules, Result> arrange)
    {
        var arranged = arrange(rules);
        Assert.True(arranged.IsSuccess, arranged.Error?.Message);
        await AppendAsync(provider, new WorkDigestDispatch(rules.TenantId, rules.Id), rules.PendingEvents);
    }
    public static Task WeeklyAsync(IServiceProvider provider, Uuid tenant, Uuid digest, WeeklyWorkDigestRules rules) =>
        WeeklyDigestFacts.WriteAsync(provider, tenant, digest, rules);
    public static async Task AppendAsync<TAggregate>(IServiceProvider provider, TAggregate owner,
        IReadOnlyList<DomainEvent> facts) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        TAggregate Fresh()
        {
            Aggregate created = owner switch
            {
                WorkDigestDispatch dispatch => new WorkDigestDispatch(dispatch.TenantId, dispatch.Id),
                WeeklyWorkDigest weekly => new WeeklyWorkDigest(weekly.TenantId, weekly.Id),
                _ => throw new InvalidOperationException("Only digest fixture owners are supported.")
            };
            return (TAggregate)created;
        }
        var current = await reader.HydrateAsync(Fresh());
        var remaining = facts.Skip(checked((int)current.Version)).ToArray();
        foreach (var fact in remaining)
        {
            if (fact is WorkDigestUnknownRetryAuthorized { Operation: null } authorization)
            {
                await AuthorizeAsync(scope.ServiceProvider, authorization, checked((long)current.Version));
            }
            else
            {
                var attached = DomainEventSeed.Attach(RoundTrip(fact), current.Id, current.Version + 1);
                await scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(current.Stream,
                    current.CommittedStreamPosition, [attached]);
            }
            current = await reader.HydrateAsync(Fresh());
        }
    }
    public static async Task AuthorizeAsync(IServiceProvider services, WorkDigestUnknownRetryAuthorized authorization,
        long expectedRevision, Uuid? commandId = null)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        var settings = services.GetRequiredService<WorkDigestDeliverySettings>();
        var operations = new WorkDigestRetryOperations(services.GetRequiredService<NativeOperationExecutor>(), content,
            new FrozenClock(authorization.AuthorizedAt), settings);
        var request = new AuthorizeWorkDigestUnknownRetry(commandId ?? Uuid.CreateVersion4(), authorization.TenantId,
            authorization.MemberId, authorization.WeekOf, authorization.EvidenceReference, authorization.Rationale, expectedRevision);
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
            new Claim("sub", authorization.AuthorizedBy.ToString())], "BdgrzSession"));
        var context = new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request,
            new RequestDispatchContext(actor, new HttpInvocation("PUT", "/fixture/retry", "/fixture/retry", "fixture")));
        var result = await operations.AuthorizeAsync(context, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
    sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    static DomainEvent RoundTrip(DomainEvent fact) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default), fact.GetType(),
        ComplianceCoreJsonContext.Default)!;
}
