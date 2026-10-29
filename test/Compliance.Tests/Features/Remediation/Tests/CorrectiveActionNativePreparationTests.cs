using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionNativePreparationTests
{
    [Fact]
    public async Task ShouldReturnEqualCompletionWinnerGivenFindingReadFailsAfterConcurrentAdmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(7));
        var actionId = Assert.Single(finding.CorrectiveActions).ActionId;
        var request = fixture.Complete(finding, actionId, "Access removed.", OperationsFixture.FullSupport);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var inner = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var clock = new MovingClock();
        FindingView? winner = null;
        var other = ActivatorUtilities.CreateInstance<CompleteCorrectiveActionHandler>(services, clock);
        var reader = new ConcurrentFindingReader(inner, FindingAggregate.StreamId(fixture.ProgramId, finding.FindingId), async () =>
        {
            clock.Now = clock.Now.AddDays(1);
            var accepted = await other.HandleAsync(new RequestContext<CompleteCorrectiveAction>(request,
                ProgramManagementServices.Actor(fixture.OwnerUserId)), CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
        });
        var handler = ActivatorUtilities.CreateInstance<CompleteCorrectiveActionHandler>(services,
            new NativeOperationExecutor(reader, executor, content), new FindingReader(reader, events, content), clock);

        // Act
        var result = await handler.HandleAsync(new RequestContext<CompleteCorrectiveAction>(request,
            ProgramManagementServices.Actor(fixture.OwnerUserId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(WireJson.Of(winner!), WireJson.Of(result.Value!));
        Assert.Equal(2, (await fixture.ActionAsync(finding.FindingId, actionId)).Revision);
        Assert.Equal(1, reader.CompetingAdmissions);
    }

    sealed class ConcurrentFindingReader(IAggregateReader inner, Uuid finding, Func<Task> admit) : IAggregateReader
    {
        bool _armed = true;
        public int CompetingAdmissions { get; private set; }
        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (_armed && aggregate is FindingAggregate && aggregate.Id == finding)
            {
                _armed = false;
                await admit();
                CompetingAdmissions++;
                return aggregate;
            }
            return await inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class MovingClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
