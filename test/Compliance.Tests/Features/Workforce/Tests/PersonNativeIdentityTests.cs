using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

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
