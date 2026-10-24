using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

/// <summary>The shared content and provenance helpers that criteria and evaluation owners freeze their operations with.</summary>
public sealed class NativeContentTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    static FitzKvImmutableContentStore Store() => new(new PersistenceKvClient(new InMemoryKvClient()));

    [Fact]
    public async Task ShouldReplayTheFrozenViewGivenCommittedAndReplayedOutcomes()
    {
        // Arrange
        var store = Store();
        var registration = new Bdgrz.Compliance.Features.Criteria.CriteriaTextOverlayRegistration(
            Uuid.CreateVersion4(), "CC6.1", 3);
        var (response, result) = await NativeContent.FreezeAsync(store, TenantId, "test.registration.v1", registration);
        var first = NativeFacts.Ref();
        var fact = new NativeFactReference(new EventStreamAddress(TenantId.ToString(), "probes", "one"),
            Uuid.CreateVersion4(), 1);

        // Act
        var committed = await NativeContent.ResponseAsync<Bdgrz.Compliance.Features.Criteria.CriteriaTextOverlayRegistration>(
            store, TenantId, Result<AcceptedAppendResult>.Success(new AcceptedAppendResult.Committed(fact, 1, first,
                response)));
        var replayed = await NativeContent.ResponseAsync<Bdgrz.Compliance.Features.Criteria.CriteriaTextOverlayRegistration>(
            store, TenantId, Result<AcceptedAppendResult>.Success(new AcceptedAppendResult.Replayed(fact, first,
                response)));
        var conflict = await NativeContent.ResponseAsync<Bdgrz.Compliance.Features.Criteria.CriteriaTextOverlayRegistration>(
            store, TenantId, Result<AcceptedAppendResult>.Success(AcceptedAppendResult.Conflict.Instance));

        // Assert
        Assert.Equal(registration, committed.Value);
        Assert.Equal(registration, replayed.Value);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal("mutation-result-content.v1", result.Schema);
    }

    [Fact]
    public async Task ShouldFreezeAnEmptyResultGivenUnitOutcome()
    {
        // Arrange
        var store = Store();
        var (response, _) = await NativeContent.FreezeUnitAsync(store, TenantId);
        var fact = new NativeFactReference(new EventStreamAddress(TenantId.ToString(), "probes", "one"),
            Uuid.CreateVersion4(), 1);

        // Act
        var committed = NativeContent.UnitOutcome(Result<AcceptedAppendResult>.Success(
            new AcceptedAppendResult.Committed(fact, 1, NativeFacts.Ref(), response)));
        var conflict = NativeContent.UnitOutcome(Result<AcceptedAppendResult>.Success(AcceptedAppendResult.Conflict.Instance));
        var failed = NativeContent.UnitOutcome(Result<AcceptedAppendResult>.Failure(
            new RequestError(RequestErrorKind.NotFound, "Missing.")));

        // Assert
        Assert.True(committed.IsSuccess);
        Assert.Equal(UnitResult.Schema, response.ResultSchema);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, failed.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReadVerifiedContentOnlyInTheTenantRealmGivenAnotherTenant()
    {
        // Arrange
        var store = Store();
        var reference = await NativeContent.WriteAsync(store, TenantId, "test.text.v1", new UnitResult());

        // Act
        var read = await NativeContent.ReadAsync<UnitResult>(store, TenantId, reference);
        var other = await Record.ExceptionAsync(() => NativeContent.ReadAsync<UnitResult>(store, Uuid.CreateVersion4(),
            reference));

        // Assert
        Assert.NotNull(read);
        Assert.IsType<ImmutableContentIntegrityException>(other);
    }

    [Fact]
    public void ShouldBindOwnerEpisodeAndSignedExpectedRevisionGivenRootProvenance()
    {
        // Arrange
        var owner = new EventStreamAddress(TenantId.ToString(), "probes", "one");
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Member");
        var logicalId = Uuid.CreateVersion4();

        // Act
        var update = RootOperation.Provenance(logicalId, "Update", owner, actor, 4, new string('d', 64), NativeFacts.Now);
        var creation = RootOperation.Provenance(logicalId, "Create", owner, actor, null, new string('d', 64),
            NativeFacts.Now);

        // Assert
        Assert.Equal(4UL, update.OriginalExpectedRevision);
        Assert.Null(creation.OriginalExpectedRevision);
        Assert.Equal((logicalId, logicalId, owner, owner, owner), (update.LogicalId, update.RootOperationId, update.Owner,
            update.AffectedOwner, update.ExpectedRevisionOwner));
        Assert.Equal(RootOperation.InitialEpisode, update.Episode);
        Assert.Throws<ArgumentOutOfRangeException>(() => RootOperation.Provenance(logicalId, "Update", owner, actor, -1,
            new string('d', 64), NativeFacts.Now));
    }
}
