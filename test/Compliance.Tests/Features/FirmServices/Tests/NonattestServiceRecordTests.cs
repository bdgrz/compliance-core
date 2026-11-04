using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.FirmServices.Tests;

public sealed class NonattestServiceRecordTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RecordId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static NonattestServiceTerms Terms => new("control_design", new DateOnly(2026, 1, 1), null);
    static RecordNonattestService Record() => new(Uuid.CreateVersion4(), TenantId, RecordId, 0, Terms);

    [Fact]
    public void ShouldRetainReasonAndOriginalFactsGivenRevisionThenVoid()
    {
        // Arrange
        var request = Record();
        var owner = new NonattestServiceRecord(TenantId, RecordId);
        Assert.True(owner.Record(request, UserId, Now).IsSuccess);
        var revision = new ReviseNonattestService(Uuid.CreateVersion4(), TenantId, RecordId, 1,
            Terms with { EndedOn = new DateOnly(2026, 7, 1) }, "Actual completion");

        // Act
        var changed = owner.Revise(revision, UserId, Now.AddDays(1));
        var ended = owner.Void(new(Uuid.CreateVersion4(), TenantId, RecordId, 2, "Wrong client"),
            UserId, Now.AddDays(2));

        // Assert
        Assert.True(changed.IsSuccess);
        Assert.True(ended.IsSuccess);
        Assert.True(owner.View!.IsVoided);
        Assert.Equal(3, owner.Revision);
        Assert.Equal("Wrong client", owner.View.Reason);
        Assert.Equal(new DateOnly(2026, 7, 1), owner.View.Terms.EndedOn);
        Assert.Equal(3, new AggregateScenario<NonattestServiceRecord>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldReplayOriginalResultGivenLaterRevisionAndVoid()
    {
        // Arrange
        var request = Record();
        var source = new NonattestServiceRecord(TenantId, RecordId);
        var original = source.Record(request, UserId, Now);
        Assert.True(source.Revise(new(Uuid.CreateVersion4(), TenantId, RecordId, 1,
            Terms with { ServiceKind = "vciso" }, "Correction"), UserId, Now.AddDays(1)).IsSuccess);
        Assert.True(source.Void(new(Uuid.CreateVersion4(), TenantId, RecordId, 2, "Wrong client"),
            UserId, Now.AddDays(2)).IsSuccess);
        var restored = NativeFacts.Replay(new NonattestServiceRecord(TenantId, RecordId, request.CommandId),
            NativeFacts.Raised(source));

        // Act
        var retry = restored.Record(request, UserId, Now.AddDays(5));
        var changed = restored.Record(request with { Terms = Terms with { ServiceKind = "vciso" } },
            UserId, Now.AddDays(5));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(original.Value, retry.Value);
        Assert.False(changed.IsSuccess);
        Assert.Equal(3, restored.Revision);
        Assert.Empty(new AggregateScenario<NonattestServiceRecord>(restored).PendingEvents);
    }

    [Theory]
    [InlineData("unknown", 0)]
    [InlineData("control_design", 1)]
    public void ShouldRefuseWithoutFactGivenInvalidKindOrExpectedRevision(string kind, long expected)
    {
        // Arrange
        var request = Record() with { ExpectedRevision = expected, Terms = Terms with { ServiceKind = kind } };
        var owner = new NonattestServiceRecord(TenantId, RecordId);

        // Act
        var result = owner.Record(request, UserId, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(owner.View);
        Assert.Empty(new AggregateScenario<NonattestServiceRecord>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutMutationGivenCompetingCorrectionAtSameRevision()
    {
        // Arrange
        var owner = new NonattestServiceRecord(TenantId, RecordId);
        Assert.True(owner.Record(Record(), UserId, Now).IsSuccess);
        var first = new ReviseNonattestService(Uuid.CreateVersion4(), TenantId, RecordId, 1,
            Terms with { ServiceKind = "vciso" }, "Correction");
        Assert.True(owner.Revise(first, UserId, Now.AddDays(1)).IsSuccess);
        var accepted = owner.View;

        // Act
        var competing = owner.Revise(first with { CommandId = Uuid.CreateVersion4(),
            Terms = Terms with { ServiceKind = "control_implementation" } }, UserId, Now.AddDays(2));

        // Assert
        Assert.False(competing.IsSuccess);
        Assert.Equal(accepted, owner.View);
        Assert.Equal(2, new AggregateScenario<NonattestServiceRecord>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldLeaveRevisionUnchangedGivenUnchangedTerms()
    {
        // Arrange
        var owner = new NonattestServiceRecord(TenantId, RecordId);
        Assert.True(owner.Record(Record(), UserId, Now).IsSuccess);

        // Act
        var result = owner.Revise(new(Uuid.CreateVersion4(), TenantId, RecordId, 1, Terms, "Verified"),
            UserId, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, owner.Revision);
        Assert.Single(new AggregateScenario<NonattestServiceRecord>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseNewRevisionGivenVoidedRecord()
    {
        // Arrange
        var owner = new NonattestServiceRecord(TenantId, RecordId);
        Assert.True(owner.Record(Record(), UserId, Now).IsSuccess);
        Assert.True(owner.Void(new(Uuid.CreateVersion4(), TenantId, RecordId, 1, "Wrong client"), UserId,
            Now.AddDays(1)).IsSuccess);

        // Act
        var result = owner.Revise(new(Uuid.CreateVersion4(), TenantId, RecordId, 2,
            Terms with { ServiceKind = "vciso" }, "Correction"), UserId, Now.AddDays(2));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, new AggregateScenario<NonattestServiceRecord>(owner).PendingEvents.Count);
    }
    [Fact]
    public void ShouldRetainExactOriginalRevisionGivenLaterCorrectionAndVoid()
    {
        // Arrange
        var source = new NonattestServiceRecord(TenantId, RecordId);
        Assert.True(source.Record(Record(), UserId, Now).IsSuccess);
        Assert.True(source.Revise(new(Uuid.CreateVersion4(), TenantId, RecordId, 1,
            Terms with { ServiceKind = "vciso" }, "Correction"), UserId, Now.AddDays(1)).IsSuccess);
        Assert.True(source.Void(new(Uuid.CreateVersion4(), TenantId, RecordId, 2, "Wrong client"),
            UserId, Now.AddDays(2)).IsSuccess);

        // Act
        var owner = NativeFacts.Replay(new NonattestServiceRecord(TenantId, RecordId, selectedRevision: 1),
            NativeFacts.Raised(source));

        // Assert
        Assert.True(owner.View!.IsVoided);
        Assert.Equal(1, owner.SelectedRevisionView!.Revision);
        Assert.Equal(Terms, owner.SelectedRevisionView.Terms);
        Assert.False(owner.SelectedRevisionView.IsVoided);
        Assert.Equal(Now, owner.SelectedRevisionView.RecordedAt);
    }

}
