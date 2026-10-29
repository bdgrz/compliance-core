using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class FirmStaffRecordAggregateTests
{
    static readonly ImmutableContentRef Content = new(Uuid.Parse("00000000-0000-4000-8000-0000000000a1",
        System.Globalization.CultureInfo.InvariantCulture), new string('a', 64), 2, "application/json", "test.v1");

    [Fact]
    public void ShouldKeepRecordIdentityStableGivenStaffMemberId()
    {
        // Arrange
        var staffId = Uuid.CreateVersion4();

        // Act
        var first = new FirmStaffRecord(staffId);
        var retry = new FirmStaffRecord(staffId, Uuid.CreateVersion4());
        var other = new FirmStaffRecord(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, retry.Stream);
        Assert.Equal(first.Id, retry.Id);
        Assert.NotEqual(first.Stream, other.Stream);
    }

    [Fact]
    public void ShouldRefuseRegistrationWithoutFactGivenForeignRootOrOwner()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var provenance = Provenance(owner);
        var wrongRoot = provenance with { RootOperationId = Uuid.CreateVersion4() };
        var foreignOwner = provenance with { Owner = new FirmStaffRecord(Uuid.CreateVersion4()).Stream };

        // Act
        var wrongRootResult = owner.AcceptRegistration(wrongRoot, Content, Content);
        var foreignOwnerResult = owner.AcceptRegistration(foreignOwner, Content, Content);

        // Assert
        Assert.False(wrongRootResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, wrongRootResult.Error!.Kind);
        Assert.False(foreignOwnerResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, foreignOwnerResult.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Accepted);
    }

    [Fact]
    public void ShouldRefuseRegistrationWithoutFactGivenStaleRevision()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var provenance = Provenance(owner) with { OriginalExpectedRevision = 1 };

        // Act
        var result = owner.AcceptRegistration(provenance, Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseRegistrationWithoutFactGivenEmptyStaffIdentity()
    {
        // Arrange
        var owner = new FirmStaffRecord(Uuid.Empty);
        var scenario = new AggregateScenario<FirmStaffRecord>(owner);

        // Act
        var result = owner.AcceptRegistration(Provenance(owner), Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Accepted);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("cause")]
    [InlineData("actor")]
    [InlineData("episode")]
    [InlineData("first_owner")]
    [InlineData("affected_owner")]
    [InlineData("revision_owner")]
    [InlineData("hash")]
    [InlineData("accepted_at")]
    public void ShouldRefuseRegistrationWithoutFactGivenInvalidRootProvenance(string defect)
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var operation = Provenance(owner);
        var foreign = new FirmStaffRecord(Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "kind" => operation with { CommandKind = "Other" },
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "actor" => operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Member") },
            "episode" => operation with { Episode = "other" },
            "first_owner" => operation with { FirstOwner = foreign },
            "affected_owner" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            "hash" => operation with { AuthoredIntentSha256 = "invalid" },
            "accepted_at" => operation with { AcceptedAt = default },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        // Act
        var result = owner.AcceptRegistration(operation, Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Accepted);
    }

    [Fact]
    public void ShouldRefuseCompletionAndRejectionWithoutFactGivenDifferentRootOrOwner()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var provenance = Provenance(owner);
        Assert.True(owner.AcceptRegistration(provenance, Content, Content).IsSuccess);
        var wrongRoot = provenance with { RootOperationId = Uuid.CreateVersion4(), LogicalId = Uuid.CreateVersion4() };
        var foreignOwner = provenance with { Owner = new FirmStaffRecord(Uuid.CreateVersion4()).Stream };

        // Act
        var completeWrongRoot = owner.CompleteRegistration(wrongRoot, Content, Content);
        var rejectWrongRoot = owner.RejectRegistration(wrongRoot, Content);
        var completeForeign = owner.CompleteRegistration(foreignOwner, Content, Content);
        var rejectForeign = owner.RejectRegistration(foreignOwner, Content);

        // Assert
        Assert.False(completeWrongRoot.IsSuccess);
        Assert.False(rejectWrongRoot.IsSuccess);
        Assert.False(completeForeign.IsSuccess);
        Assert.False(rejectForeign.IsSuccess);
        Assert.IsType<FirmStaffRegistrationAccepted>(Assert.Single(scenario.PendingEvents));
        Assert.True(owner.IsPending);
        Assert.Equal(0UL, owner.Revision);
    }

    [Fact]
    public async Task ShouldRefuseCompletionAndRejectionGivenVerifiedCutForAnotherRootOrOwner()
    {
        // Arrange
        var staffId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var seed = new FirmStaffRecord(staffId);
        Assert.True(seed.AcceptRegistration(Provenance(seed), Content, Content).IsSuccess);
        var facts = NativeFacts.Raised(seed);
        var owner = NativeFacts.Replay(new FirmStaffRecord(staffId, logicalId), facts);
        var scenario = new AggregateScenario<FirmStaffRecord>(owner);
        await FirmStaffHistory.ReadAsync(new Records(owner.Stream, facts), owner);
        var provenance = Provenance(owner) with { LogicalId = logicalId };
        var wrongRoot = provenance with { RootOperationId = Uuid.CreateVersion4() };
        var foreignOwner = provenance with { Owner = new FirmStaffRecord(Uuid.CreateVersion4()).Stream };

        // Act
        var completionWrongRoot = owner.CompleteRegistration(wrongRoot, Content, Content);
        var rejectionWrongRoot = owner.RejectRegistration(wrongRoot, Content);
        var completionForeign = owner.CompleteRegistration(foreignOwner, Content, Content);
        var rejectionForeign = owner.RejectRegistration(foreignOwner, Content);

        // Assert
        Assert.False(completionWrongRoot.IsSuccess);
        Assert.False(rejectionWrongRoot.IsSuccess);
        Assert.False(completionForeign.IsSuccess);
        Assert.False(rejectionForeign.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.IsPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAcceptTerminalDecisionGivenValidChildProvenance(bool reject)
    {
        // Arrange
        var (owner, scenario, operation) = await VerifiedPendingAsync(reject);

        // Act
        var result = reject
            ? owner.RejectRegistration(operation, Content)
            : owner.CompleteRegistration(operation, Content, Content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.Equal(reject, owner.IsRejected);
        Assert.Equal(!reject, owner.IsRegistered);
    }

    [Theory]
    [InlineData(false, "cause")]
    [InlineData(false, "kind")]
    [InlineData(false, "actor")]
    [InlineData(false, "episode")]
    [InlineData(false, "first_owner")]
    [InlineData(false, "affected_owner")]
    [InlineData(false, "revision_owner")]
    [InlineData(false, "expected")]
    [InlineData(false, "hash")]
    [InlineData(true, "cause")]
    [InlineData(true, "kind")]
    [InlineData(true, "actor")]
    [InlineData(true, "episode")]
    [InlineData(true, "first_owner")]
    [InlineData(true, "affected_owner")]
    [InlineData(true, "revision_owner")]
    [InlineData(true, "expected")]
    [InlineData(true, "hash")]
    public async Task ShouldRefuseTerminalDecisionWithoutFactGivenInvalidChildProvenance(bool reject, string defect)
    {
        // Arrange
        var (owner, scenario, operation) = await VerifiedPendingAsync(reject);
        var foreign = new FirmStaffRecord(Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "kind" => operation with { CommandKind = "Other" },
            "actor" => operation with { Actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Other") },
            "episode" => operation with { Episode = "other" },
            "first_owner" => operation with { FirstOwner = foreign },
            "affected_owner" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            "expected" => operation with { OriginalExpectedRevision = 1 },
            "hash" => operation with { AuthoredIntentSha256 = new string('e', 64) },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        // Act
        var result = reject
            ? owner.RejectRegistration(operation, Content)
            : owner.CompleteRegistration(operation, Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.IsPending);
    }

    [Fact]
    public void ShouldRefuseStatusAndUseWithoutFactGivenUnregisteredRecord()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var provenance = Provenance(owner);

        // Act
        var status = owner.SetStatus(provenance, "status", Content, Content, Content);
        var duty = owner.AuthorizeProfessionalDutyUse(provenance, Content, 1);
        var engagement = owner.AuthorizeEngagementUse(provenance, Content, 1);

        // Assert
        Assert.False(status.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, status.Error!.Kind);
        Assert.False(duty.IsSuccess);
        Assert.False(engagement.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    static FirmStaffRecord Fresh(out AggregateScenario<FirmStaffRecord> scenario)
    {
        var owner = new FirmStaffRecord(Uuid.CreateVersion4());
        scenario = new AggregateScenario<FirmStaffRecord>(owner);
        return owner;
    }

    static OperationProvenance Provenance(FirmStaffRecord owner) =>
        new(owner.StaffMemberId, owner.StaffMemberId, null, "RegisterFirmStaff",
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator"), owner.Stream, "initial", 0,
            new string('d', 64), DateTimeOffset.UtcNow, owner.Stream, owner.Stream);

    static async Task<(FirmStaffRecord Owner, AggregateScenario<FirmStaffRecord> Scenario, OperationProvenance Operation)>
        VerifiedPendingAsync(bool reject)
    {
        var staffId = Uuid.CreateVersion4();
        var childId = Uuid.CreateVersion4();
        var seed = new FirmStaffRecord(staffId);
        Assert.True(seed.AcceptRegistration(Provenance(seed), Content, Content).IsSuccess);
        var facts = NativeFacts.Raised(seed);
        var owner = NativeFacts.Replay(new FirmStaffRecord(staffId, childId), facts);
        var scenario = new AggregateScenario<FirmStaffRecord>(owner);
        await FirmStaffHistory.ReadAsync(new Records(owner.Stream, facts), owner);
        var root = owner.Accepted!;
        var operation = root.Operation with
        {
            LogicalId = childId,
            CausationEventId = root.Metadata.EventId,
            CommandKind = reject ? "RejectFirmStaffRegistration" : "CompleteFirmStaffRegistration",
            FirstOwner = owner.Stream,
        };
        return (owner, scenario, operation);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);

        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
