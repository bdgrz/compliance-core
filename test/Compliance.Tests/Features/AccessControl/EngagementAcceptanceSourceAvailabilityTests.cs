using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.AcceptanceFacts;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementAcceptanceSourceAvailabilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldPreservePendingAuthorizationGivenUnavailablePinnedSourceAndRejectCorruption(bool unavailable)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var request = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        var record = await StartAsync(host, engagement, request.CommandId);
        var facts = new Dictionary<string, NativeFactReference>();
        foreach (var target in record.Targets)
            foreach (var receiver in EngagementAcceptanceProcess.Definition().Stages[0].Receivers)
            {
                if (await BuildAsync(host, record, receiver, target) is not { } command)
                    continue;
                var held = Assert.IsType<ReceiverOutcome.Applied>((await SendAsync(host, command)).Value);
                facts[ProcessRecord.FactKey(receiver, target)] = held.Fact;
            }
        record = record with { Facts = facts };
        var authorize = Assert.IsType<AuthorizeEngagementPracticeCommitments>(
            await BuildAsync(host, record, nameof(AuthorizeEngagementPracticeCommitments)));
        var services = host.Scope.ServiceProvider;
        var content = new PinnedSourceFault(host.Content, unavailable);
        var handler = new AuthorizeEngagementPracticeCommitmentsHandler(
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateExecutor>(),
            content, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<EngagementAcceptanceVerifier>(),
            services.GetRequiredService<IPlatformUserDirectoryReader>(), services.GetRequiredService<BoundaryReader>());
        var context = new RequestContext<AuthorizeEngagementPracticeCommitments>(authorize, RequestActor.System);
        var owner = new ServiceEngagementIndependence(host.Tenant, engagement);
        var before = (await host.HydrateAsync(owner)).CommittedStreamPosition;

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));

        // Assert
        Assert.True(content.ReadAttempted);
        Assert.Equal(before, after.CommittedStreamPosition);
        if (unavailable)
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.True(result.Error?.IsTransient);
            content.Enabled = false;
            var retried = await handler.HandleAsync(context, CancellationToken.None);
            Assert.True(retried.IsSuccess, retried.Error?.Message);
            Assert.IsType<ReceiverOutcome.Applied>(retried.Value);
        }
        else
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.IsType<ReceiverOutcome.Rejected>(result.Value);
        }
    }

    sealed class PinnedSourceFault(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public bool Enabled { get; set; } = true;
        public bool ReadAttempted { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (!Enabled || reference.Schema is not (ProfessionalDutyContent.RecordedSchema or FirmStaffContent.StaffSchema))
                return inner.ReadVerifiedAsync(realm, reference, ct);
            ReadAttempted = true;
            return Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact pinned source body is not visible yet.")
                : new ImmutableContentIntegrityException("The exact pinned source body is corrupt."));
        }
    }
}
