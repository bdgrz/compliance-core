using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRequestNativePreparationTests
{
    [Fact]
    public async Task ShouldReturnEqualWinnerGivenOwnerMembershipGuardFailsAfterConcurrentOpenAdmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var inner = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var controls = services.GetRequiredService<ControlReader>();
        var clock = new MovingClock();
        var request = new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            "Original request", "Collect the exact evidence.", fixture.OwnerMemberId, fixture.Today.AddDays(30));
        EvidenceRequestView? winner = null;
        var other = new OpenEvidenceRequestHandler(new NativeOperationExecutor(inner, executor, content), inner, controls, content, clock);
        var reader = new ConcurrentOwnerMembershipReader(inner, fixture.OwnerMemberId, async () =>
        {
            clock.Now = clock.Now.AddDays(1);
            var accepted = await other.HandleAsync(new RequestContext<OpenEvidenceRequest>(request,
                ProgramManagementServices.Actor(fixture.LeadUserId)), CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
        });
        var handler = new OpenEvidenceRequestHandler(new NativeOperationExecutor(reader, executor, content), reader, controls, content, clock);

        // Act
        var result = await handler.HandleAsync(new RequestContext<OpenEvidenceRequest>(request,
            ProgramManagementServices.Actor(fixture.LeadUserId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(winner, result.Value);
        Assert.Equal(1, (await inner.HydrateAsync(new EvidenceRequest(fixture.TenantId, fixture.ProgramId,
            request.EvidenceRequestId))).Revision);
        Assert.Equal(1, reader.CompetingAdmissions);
    }

    sealed class ConcurrentOwnerMembershipReader(IAggregateReader inner, Uuid owner, Func<Task> admit) : IAggregateReader
    {
        bool _armed = true;
        public int CompetingAdmissions { get; private set; }
        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (_armed && aggregate is Member && aggregate.Id == owner)
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
