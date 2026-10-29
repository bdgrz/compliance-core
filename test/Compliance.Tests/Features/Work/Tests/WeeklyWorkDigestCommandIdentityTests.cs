using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyWorkDigestCommandIdentityTests
{
    [Fact]
    public async Task ShouldRefuseChangedOwnerRevisionGivenAlreadyAcceptedDeliveryCommandIdentity()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var schedule = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var command = Uuid.CreateVersion4();
        var first = Delivery(fixture.TenantId, member, digest, schedule, command, 1, 1);
        var handler = fixture.DeliveryHandler();
        var accepted = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(first, schedule), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var changed = Delivery(fixture.TenantId, member, digest, schedule, command, 999, 1);

        // Act
        var result = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(changed, schedule), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId, digest));
        Assert.Equal(2, owner.Revision);
        Assert.Equal("delivery_requested", owner.Status);
    }

    [Fact]
    public async Task ShouldRefuseReusedResultCommandGivenDifferentActualDeliveryAdmissionOnSameDigest()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var schedule = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var deliver = fixture.DeliveryHandler();
        var record = fixture.RecordHandler();
        var first = Delivery(fixture.TenantId, member, digest, schedule, Uuid.CreateVersion4(), 1, 1);
        Assert.True((await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(first, schedule), CancellationToken.None)).IsSuccess);
        var firstSource = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digest);
        var command = Uuid.CreateVersion4();
        var completedAt = fixture.Clock.GetUtcNow().AddMinutes(1);
        fixture.Clock.Set(completedAt);
        var failed = Recorded(fixture.TenantId, digest, firstSource, command, 2, 1, "failed", completedAt, "provider_busy");
        Assert.True((await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(failed, firstSource), CancellationToken.None)).IsSuccess);
        fixture.Clock.Set(completedAt.AddMinutes(1));
        var next = Delivery(fixture.TenantId, member, digest, schedule, Uuid.CreateVersion4(), 3, 2);
        Assert.True((await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(next, schedule), CancellationToken.None)).IsSuccess);
        var secondSource = Uuid.Empty;
        await foreach (var source in fixture.Events.ReadAsync(new WeeklyWorkDigest(fixture.TenantId, digest).Stream, 0, CancellationToken.None))
            if (source.Event is WeeklyWorkDigestDeliveryRequested { AttemptNumber: 2 })
                secondSource = source.Event.Metadata.EventId;
        Assert.NotEqual(Uuid.Empty, secondSource);
        fixture.Clock.Set(completedAt.AddMinutes(2));
        var reused = Recorded(fixture.TenantId, digest, secondSource, command, 4, 2, "delivered", fixture.Clock.GetUtcNow(), null);

        // Act
        var result = await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(reused, secondSource), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId, digest));
        Assert.Equal(4, owner.Revision);
        Assert.Equal("delivery_requested", owner.Status);
    }

    static DeliverWeeklyWorkDigest Delivery(Uuid tenant, Uuid member, Uuid digest, Uuid source, Uuid command,
        long expected, int attempt)
    {
        // Extra selected CommandId is raw JSON so the same regression executes against the old DTO.
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{tenant}}","member_id":"{{member}}","digest_id":"{{digest}}","schedule_event_id":"{{source}}","expected_revision":{{expected}},"attempt_number":{{attempt}},"delivery_key":"{{WeeklyWorkDigest.DeliveryKeyFor(digest, attempt)}}"}""";
        return JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.DeliverWeeklyWorkDigest)!;
    }
    static RecordWorkDigestDelivery Recorded(Uuid tenant, Uuid digest, Uuid source, Uuid command,
        long expected, int attempt, string outcome, DateTimeOffset at, string? failure)
    {
        var failureJson = failure is null ? "null" : "\"" + failure + "\"";
        var json = $$"""{"command_id":"{{command}}","tenant_id":"{{tenant}}","digest_id":"{{digest}}","delivery_request_event_id":"{{source}}","expected_revision":{{expected}},"attempt_number":{{attempt}},"delivery_key":"{{WeeklyWorkDigest.DeliveryKeyFor(digest, attempt)}}","outcome":"{{outcome}}","attempted_at":"{{at:O}}","failure_code":{{failureJson}}}""";
        return JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.RecordWorkDigestDelivery)!;
    }
}
