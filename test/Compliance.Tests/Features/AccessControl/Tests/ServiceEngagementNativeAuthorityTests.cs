using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ServiceEngagementNativeAuthorityTests
{
    [Fact]
    public async Task ShouldKeepBoundedEngagementAuthorityGivenManyAcceptedLifecycleChanges()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);
        for (var revision = 1; revision <= 32; revision++)
            Assert.True((await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
                engagement, revision, IndependenceHost.Draft(lead, "Scope " + revision)))).IsSuccess);

        // Act
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var retained = typeof(ServiceEngagementIndependence).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is System.Collections.ICollection collection && collection.Count > 2)
            .Select(field => field.Name).ToArray();
        var history = await host.SendAsync(new GetServiceEngagementHistory(host.Tenant, engagement));

        // Assert
        Assert.Equal(33UL, owner.Revision);
        Assert.Equal("draft", owner.Status);
        Assert.Equal(33, history.Value!.Count);
        Assert.Empty(retained);
    }

    [Fact]
    public async Task ShouldFenceNewPersonalDecisionGivenAcceptanceAlreadyPinsEngagementRevision()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await AcceptanceFacts.ReadyAsync(host);
        var accept = AcceptanceFacts.AcceptCommand(host, engagement);
        var admitted = await host.HttpAsync(accept, partner.Actor);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var before = await host.EventsAsync<DomainEvent>(new ServiceEngagementIndependence(host.Tenant, engagement).Stream);
        var acknowledge = await PersonalDecisionFacts.AcknowledgementAsync(host, engagement);

        // Act
        var refused = await host.HttpAsync(acknowledge);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.Equal(before.Count, (await host.EventsAsync<DomainEvent>(new ServiceEngagementIndependence(host.Tenant, engagement).Stream)).Count);
    }

    [Fact]
    public async Task ShouldReplayEqualLifecycleWinnerGivenLosingCandidateWriteUnavailable()
    {
        // Arrange
        LosingEngagementStore content = null!;
        await using var host = new IndependenceHost(configureContent: inner => content = new LosingEngagementStore(inner));
        var lead = await host.RegisterStaffAsync();
        var engagement = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)))).IsSuccess);
        var command = new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Authored amendment"));
        string? winning = null;
        content.Arm(async () =>
        {
            var winner = await host.SendAsync(command);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            winning = IndependenceHost.Json(winner.Value);
        });
        Result<ServiceEngagementView>? actual = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await host.SendAsync(command));

        // Assert
        Assert.True(content.Failed);
        Assert.NotNull(winning);
        Assert.Null(failure);
        Assert.True(actual!.Value.IsSuccess, actual.Value.Error?.Message);
        Assert.Equal(winning, IndependenceHost.Json(actual.Value.Value));
        Assert.Equal(2, (await host.EventsAsync<DomainEvent>(new ServiceEngagementIndependence(host.Tenant, engagement).Stream)).Count);
    }

    sealed class LosingEngagementStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        Func<Task>? _winner;
        public bool Failed { get; private set; }
        public void Arm(Func<Task> winner) => _winner = winner;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && _winner is { } winner && schema == IndependenceContent.EngagementChangeSchema)
            {
                Failed = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing engagement candidate publication is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
