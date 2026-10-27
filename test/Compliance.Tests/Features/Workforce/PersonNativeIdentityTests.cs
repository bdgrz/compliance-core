using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class PersonNativeIdentityTests
{
    [Fact]
    public async Task ShouldUseExactPersonCreationIdentityGivenDifferentRequestDispatches()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            tenant_id = fixture.TenantId,
            person_id = personId,
            display_name = "Grace Hopper",
            work_email = "grace@example.com"
        }), ComplianceCoreJsonContext.Default.RecordPerson)!;

        // Act
        var first = await fixture.BusAsync(request);
        var retry = await fixture.BusAsync(request);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(personId, first.Value.PersonId);
        Assert.Equal(first.Value, retry.Value);
        var facts = await FactsAsync(fixture.Provider, new Person(fixture.TenantId, personId).Stream);
        Assert.Single(facts);
    }

    [Fact]
    public async Task ShouldReplayOriginalRevisionReceiptGivenConsumedExpectedPersonRevision()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            command_id = Uuid.CreateVersion4(),
            tenant_id = fixture.TenantId,
            person_id = fixture.PersonId,
            expected_revision = 1L,
            display_name = "Ada King",
            work_email = "ada@example.com"
        }), ComplianceCoreJsonContext.Default.RevisePerson)!;
        await using var scope = fixture.Provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(fixture.UserId);
        var first = await bus.SendAsync(request, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await FactsAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId).Stream);

        // Act
        var retry = await bus.SendAsync(request, actor);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before.Count, (await FactsAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId).Stream)).Count);
    }

    [Fact]
    public async Task ShouldReplayOriginalMembershipCorrelationGivenConsumedExpectedPersonRevision()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            command_id = Uuid.CreateVersion4(),
            tenant_id = fixture.TenantId,
            person_id = fixture.PersonId,
            expected_revision = 1L,
            user_id = fixture.UserId
        }), ComplianceCoreJsonContext.Default.CorrelatePersonMembership)!;
        await using var scope = fixture.Provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(fixture.UserId);
        var first = await bus.SendAsync(request, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await FactsAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId).Stream);

        // Act
        var retry = await bus.SendAsync(request, actor);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before.Count, (await FactsAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId).Stream)).Count);
    }

    [Fact]
    public async Task ShouldKeepActualNativePersonFactWithinFrameGivenLargeValidPersonalContact()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(new
        {
            tenant_id = fixture.TenantId,
            person_id = Uuid.CreateVersion4(),
            display_name = "Grace Hopper",
            personal_contact = new { personal_phone = new string('7', 450_000) }
        }), ComplianceCoreJsonContext.Default.RecordPerson)!;

        // Act
        var result = await fixture.BusAsync(request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var facts = await FactsAsync(fixture.Provider, new Person(fixture.TenantId, result.Value.PersonId).Stream);
        Assert.Single(facts);
        Assert.All(facts, fact => Assert.True(DomainEventPayload.Size(fact) <= DomainEventPayload.MaximumBytes,
            $"The actual native person fact is {DomainEventPayload.Size(fact)} bytes."));
    }

    static async Task<IReadOnlyList<DomainEvent>> FactsAsync(IServiceProvider services, EventStreamAddress stream)
    {
        await using var scope = services.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var facts = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(stream, 0))
            facts.Add(record.Event);
        return facts;
    }
}
