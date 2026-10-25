using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmStaffRetainedStatusTests
{
    [Fact]
    public async Task ShouldVerifyOriginalStatusGivenLaterStaffChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var staffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator");
        var recordedAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        await FirmStaffFacts.RegisterAsync(provider, staffId, Uuid.CreateVersion4(), "attest",
            "Explicit test directory source", actor, recordedAt);
        await FirmStaffFacts.SetStatusAsync(provider, staffId, false, "Original status source",
            recordedAt.AddMinutes(1));
        await FirmStaffFacts.SetStatusAsync(provider, staffId, true, "Later status source", recordedAt.AddMinutes(2));
        var cause = await ReadStatusChangeAsync(provider, staffId, "Original status source");

        // Act
        var retained = await ProgramManagementServices.HydrateAsync(provider, new FirmStaffRecord(staffId));

        await using var scope = provider.CreateAsyncScope();
        var history = await FirmStaffHistory.ReadAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), retained);

        // Assert
        Assert.Equal(3UL, retained.Revision);
        Assert.True(history.Matches(cause));
        Assert.False(Matches(cause with { Reason = "Another status reason" }));
        Assert.False(Matches(cause with { ChangeKind = "register" }));
        Assert.False(Matches(cause with { Revision = 3 }));
        Assert.False(Matches(cause with
        {
            StaffContent = cause.StaffContent with { Sha256 = new string('f', 64) },
        }));
        Assert.False(Matches(cause with
        {
            Operation = cause.Operation with { LogicalId = Uuid.CreateVersion4() },
        }));

        bool Matches(FirmStaffChangeRecorded candidate)
        {
            candidate.AttachMetadata(cause.Metadata);
            return history.Matches(candidate);
        }
    }

    static async Task<FirmStaffChangeRecorded> ReadStatusChangeAsync(IServiceProvider provider, Uuid staffId,
        string reason)
    {
        await using var scope = provider.CreateAsyncScope();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(new FirmStaffRecord(staffId).Stream, 0))
            if (record.Event is FirmStaffChangeRecorded { ChangeKind: "status" } change && change.Reason == reason)
                return change;
        throw new InvalidOperationException("The original status change was not recorded.");
    }
}
