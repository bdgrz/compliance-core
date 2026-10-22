using System.Runtime.CompilerServices;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingReadSourceTests
{
    [Fact]
    public async Task ShouldKeepProgramInventoryStableGivenDifferentAreaOrderAndOpaqueCursors()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var events = new FreshEventReader(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), reorder: true);
        var source = new FindingReadSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), events);

        // Act
        var result = await source.ReadAsync(fixture.TenantId, fixture.ProgramId, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value.Findings.Count);
    }

    [Fact]
    public async Task ShouldReadStableInventoryGivenFreshlyDeserializedEquivalentEvents()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = fixture.Raise(fixture.OwnerMemberId) with
        {
            Links = [new FindingLink("control", fixture.ControlId.ToString())],
        };
        var registration = await fixture.AsAsync(fixture.LeadUserId, request);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var events = new FreshEventReader(scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        var source = new FindingReadSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), events);

        // Act
        var result = await source.ReadAsync(fixture.TenantId, fixture.ProgramId, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(registration.FindingId, Assert.Single(result.Value.Findings).FindingId);
        Assert.Equal(2, events.PatternReads);
    }

    [Fact]
    public async Task ShouldAvoidTenantInventoryGivenOneFindingRead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var events = new FreshEventReader(scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        var source = new FindingReadSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), events);

        // Act
        var result = await source.ReadOneAsync(fixture.TenantId, fixture.ProgramId, registration.FindingId,
            DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(registration.FindingId, result.Value.FindingId);
        Assert.Equal(0, events.PatternReads);
    }

    [Fact]
    public async Task ShouldKeepProgramInventoryStableGivenConcurrentOtherProgramFinding()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var events = new FreshEventReader(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), async () =>
        {
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new FindingAggregate(fixture.TenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), registration.FindingId), finding =>
                {
                    Assert.Null(finding.Raise(registration.FindingId, new FindingSource("manual", null, null, "Other program gap"),
                        "Other program gap", "Other program gap", "medium", "Other", fixture.OwnerMemberId,
                        fixture.Today.AddDays(10), [], ActorReference.ForMember(fixture.LeadMemberId, "Lead"), DateTimeOffset.UtcNow));
                    return Result.Success;
                });
        });
        var source = new FindingReadSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), events);

        // Act
        var result = await source.ReadAsync(fixture.TenantId, fixture.ProgramId, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(registration.FindingId, Assert.Single(result.Value.Findings).FindingId);
    }

    [Fact]
    public async Task ShouldRejectWrongAggregateIdentityGivenCanonicalFindingStream()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Raise(fixture.OwnerMemberId));
        var target = new FindingAggregate(fixture.TenantId, fixture.ProgramId,
            registration.FindingId);
        var current = await ProgramManagementServices.HydrateAsync(fixture.Provider, target);
        var now = DateTimeOffset.UtcNow;
        var malformed = DomainEventSeed.Attach(new FindingRevisedV2(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, registration.FindingId, current.Revision + 1,
            "high", fixture.OwnerMemberId, fixture.Today.AddDays(10), "All", null,
            "Malformed aggregate identity.", ActorReference.ForMember(fixture.LeadMemberId,
                "Lead"), now), fixture.ProgramId, current.Version + 1, occurredOn: now);
        await fixture.Provider.GetRequiredService<IEventStore>().AppendAsync(target.Stream,
            current.Version, [malformed]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var source = new FindingReadSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>());

        // Act
        var result = await source.ReadAsync(fixture.TenantId, fixture.ProgramId, now,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    sealed class FreshEventReader(IDomainEventReader inner, Func<Task>? beforeSecondPatternRead = null,
        bool reorder = false) : IDomainEventReader
    {
        public int PatternReads { get; private set; }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset,
            CancellationToken ct = default) => inner.ReadAsync(stream, offset, ct);

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            PatternReads++;
            if (PatternReads == 2 && beforeSecondPatternRead is not null)
                await beforeSecondPatternRead();
            var records = new List<DomainEventRecord>();
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                records.Add(record);
            var ordered = reorder && PatternReads == 2
                ? records.GroupBy(static record => record.Stream).Reverse().SelectMany(static group => group)
                : records.AsEnumerable();
            var index = 0;
            foreach (var record in ordered)
            {
                if (record.Event is FindingRaised or FindingRaisedV2)
                {
                    var copy = (DomainEvent)JsonSerializer.Deserialize(JsonSerializer.Serialize(record.Event,
                        record.Event.GetType(), ComplianceCoreJsonContext.Default), record.Event.GetType(),
                        ComplianceCoreJsonContext.Default)!;
                    copy.AttachMetadata(record.Event.Metadata);
                    yield return record with { Event = copy, NextCursor = reorder ? new EventCursor($"opaque-{PatternReads}-{index++}") : record.NextCursor };
                }
                else
                    yield return record;
            }
        }
    }
}
