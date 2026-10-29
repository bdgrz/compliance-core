using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
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
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Contracts;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyWorkDigestNativeReceiverTests
{
    [Fact]
    public async Task ShouldFreezeExactPhysicalRealmGlobalSourcesAndUnitEnvelopeGivenActualNamedAdmissions()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId,
            RbacIds.Member(fixture.TenantId, user), digest, source, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        Assert.True((await fixture.DeliveryHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var admission = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digest);
        var at = fixture.Clock.GetUtcNow().AddMinutes(1);
        fixture.Clock.Set(at);
        var result = new RecordWorkDigestDelivery(Uuid.CreateVersion4(), fixture.TenantId, digest, admission,
            2, 1, request.DeliveryKey, "delivered", at, null);

        // Act
        var recorded = await fixture.RecordHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(result, admission), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);
        var requested = Assert.Single(history.Facts.OfType<WeeklyWorkDigestDeliveryRequested>());
        var completed = Assert.Single(history.Facts.OfType<WeeklyWorkDigestDeliveryRecorded>());
        var sourceRecords = new List<DomainEventRecord>();
        await foreach (var record in fixture.Events.ReadAsync(new WeeklyWorkDigest(fixture.TenantId, digest).Stream, 0, CancellationToken.None))
            sourceRecords.Add(record);
        var scheduleRecord = Assert.Single(sourceRecords, record => record.Event.Metadata.EventId == source);
        var requestRecord = Assert.Single(sourceRecords, record => record.Event.Metadata.EventId == admission);
        var descriptor = await FirstResponses.ReplayAsync(fixture.Content, fixture.TenantId.ToString(), completed.FirstResponse!);
        var envelope = await fixture.Content.ReadJsonAsync<MutationResultContent>(fixture.TenantId.ToString(), completed.OperationResult!);

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.Equal(scheduleRecord.ResourceOffset, requested.SourceProof.PhysicalPosition);
        Assert.Equal(requestRecord.ResourceOffset, completed.SourceProof.PhysicalPosition);
        Assert.Equal(EventStoreSourceFactResolver.RealmBase + requestRecord.ResourceOffset, completed.SourceProof.RealmPosition);
        Assert.Equal(EventStoreSourceFactResolver.GlobalBase + requestRecord.ResourceOffset, completed.SourceProof.GlobalPosition);
        Assert.NotEqual(completed.SourceProof.RealmPosition, completed.SourceProof.GlobalPosition);
        Assert.NotEqual(completed.SourceProof.PhysicalPosition, completed.SourceProof.RealmPosition);
        Assert.Equal(admission, completed.Operation.CausationEventId);
        Assert.Equal(completed.Operation.Owner, completed.Operation.FirstOwner);
        Assert.Equal("mutation-result-content.v1", completed.OperationResult!.Schema);
        Assert.Equal(UnitResult.Schema, envelope.Schema);
        Assert.Equal(descriptor.Body, envelope.Value);
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(completed.ResponseContent, Assert.IsType<StandardResponseBody>(descriptor.Body).Content);
    }

    [Fact]
    public async Task ShouldAdmitDistinctNoChangeReceiptsWithoutAnotherAttemptGivenCurrentRevisionAndSameBusinessEffect()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var deliver = fixture.DeliveryHandler();
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId, member, digest, source,
            1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        Assert.True((await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var admitted = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digest);
        var duplicate = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 };
        Assert.True((await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(duplicate, source), CancellationToken.None)).IsSuccess);
        var completedAt = fixture.Clock.GetUtcNow().AddMinutes(1);
        fixture.Clock.Set(completedAt);
        var completion = new RecordWorkDigestDelivery(Uuid.CreateVersion4(), fixture.TenantId, digest, admitted,
            3, 1, request.DeliveryKey, "delivered", completedAt, null);
        var record = fixture.RecordHandler();
        Assert.True((await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(completion, admitted), CancellationToken.None)).IsSuccess);

        // Act
        var sameResult = completion with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 4 };
        var noChange = await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(sameResult, admitted), CancellationToken.None);
        var replay = await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);

        // Assert
        Assert.True(noChange.IsSuccess, noChange.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(5, history.View.Revision);
        Assert.Equal("delivered", history.View.Status);
        var attempt = Assert.Single(history.View.DeliveryAttempts);
        Assert.Equal(1, attempt.Number);
        Assert.Equal(completedAt, attempt.CompletedAt);
        Assert.Equal(2, history.Facts.OfType<IWeeklyWorkDigestOperationFact>().Count(fact => fact.NoChange));
        Assert.Single(history.Facts.OfType<WeeklyWorkDigestDeliveryRequested>(), fact => !fact.NoChange);
    }

    [Fact]
    public async Task ShouldRejectNoChangeRequestAsDeliveryAdmissionGivenDifferentResultCommand()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId, member, digest, source,
            1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        var handler = fixture.DeliveryHandler();
        Assert.True((await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        Assert.True((await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(
            request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 }, source), CancellationToken.None)).IsSuccess);
        var receipt = Uuid.Empty;
        await foreach (var record in fixture.Events.ReadAsync(new WeeklyWorkDigest(fixture.TenantId, digest).Stream, 0, CancellationToken.None))
            if (record.Event is WeeklyWorkDigestDeliveryRequested { NoChange: true })
                receipt = record.Event.Metadata.EventId;
        Assert.NotEqual(Uuid.Empty, receipt);
        var at = fixture.Clock.GetUtcNow().AddMinutes(1);
        fixture.Clock.Set(at);
        var callback = new RecordWorkDigestDelivery(Uuid.CreateVersion4(), fixture.TenantId, digest, receipt,
            3, 1, request.DeliveryKey, "delivered", at, null);

        // Act
        var rejected = await fixture.RecordHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(callback, receipt), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error?.Kind);
        Assert.Equal(3, history.View.Revision);
        Assert.Equal("requested", Assert.Single(history.View.DeliveryAttempts).Status);
        Assert.Empty(history.Facts.OfType<WeeklyWorkDigestDeliveryRecorded>());
    }

    [Fact]
    public async Task ShouldReplayOriginalBeforeRecipientChangesGivenAcceptedDeliveryAndResultCommands()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId, member, digest, source,
            1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        var deliver = fixture.DeliveryHandler();
        Assert.True((await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var admitted = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digest);
        var at = fixture.Clock.GetUtcNow().AddMinutes(1);
        fixture.Clock.Set(at);
        var result = new RecordWorkDigestDelivery(Uuid.CreateVersion4(), fixture.TenantId, digest, admitted,
            2, 1, request.DeliveryKey, "failed", at, "provider_busy");
        var record = fixture.RecordHandler();
        Assert.True((await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(result, admitted), CancellationToken.None)).IsSuccess);
        var preference = new WorkDigestPreference(fixture.TenantId, member);
        preference.Set(false, ActorReference.ForMember(member, "Member"), at.AddMinutes(1));
        await fixture.Writer.SaveAsync(preference, new RequestDispatchContext(RequestActor.System));
        fixture.Clock.Set(DateTimeOffset.MinValue);

        // Act
        var deliveryReplay = await deliver.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None);
        var resultReplay = await record.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.RecordContext(result, admitted), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);

        // Assert
        Assert.True(deliveryReplay.IsSuccess, deliveryReplay.Error?.Message);
        Assert.True(resultReplay.IsSuccess, resultReplay.Error?.Message);
        Assert.Equal(3, history.View.Revision);
        Assert.Equal("delivery_failed", history.View.Status);
        Assert.Equal("failed", Assert.Single(history.View.DeliveryAttempts).Status);
    }
}
