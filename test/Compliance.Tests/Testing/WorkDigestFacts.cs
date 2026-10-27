using System.Text.Json;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Work.Services;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
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
