using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

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
