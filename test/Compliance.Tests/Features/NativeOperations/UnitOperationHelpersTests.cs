using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class UnitOperationHelpersTests
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");
    static readonly RequestError NoOp = new(RequestErrorKind.Conflict, "A named terminal outcome.", false);

    [Fact]
    public async Task ShouldFreezeAnEmptyHttp200ResultGivenAUnitOperation()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));

        // Act
        var descriptor = await UnitResponses.FreezeAsync(content, "bdgrz");

        // Assert
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(UnitResult.Schema, descriptor.ResultSchema);
        var body = Assert.IsType<StandardResponseBody>(descriptor.Body);
        Assert.Equal(UnitResult.Schema, body.Content.Schema);
        Assert.NotNull(await content.ReadJsonAsync<UnitResult>("bdgrz", body.Content));
    }

    [Fact]
    public void ShouldSucceedGivenACommittedOrReplayedAcceptance()
    {
        // Arrange
        var fact = new NativeFactReference(new EventStreamAddress("bdgrz", "probes", "a"), Uuid.CreateVersion4(), 1);
        var content = new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json",
            UnitResult.Schema);
        var descriptor = new FirstResponseDescriptor(200, "application/json", UnitResult.Schema,
            new StandardResponseBody(content));

        // Act
        var committed = UnitResponses.ToResult(Result<AcceptedAppendResult>.Success(
            new AcceptedAppendResult.Committed(fact, 1, content, descriptor)));
        var replayed = UnitResponses.ToResult(Result<AcceptedAppendResult>.Success(
            new AcceptedAppendResult.Replayed(fact, content, descriptor)));

        // Assert
        Assert.True(committed.IsSuccess);
        Assert.True(replayed.IsSuccess);
    }

    [Fact]
    public void ShouldConflictGivenAChangedBindingAcceptance()
    {
        // Arrange
        var changed = Result<AcceptedAppendResult>.Success(AcceptedAppendResult.Conflict.Instance);

        // Act
        var result = UnitResponses.ToResult(changed);

        // Assert
        Assert.Equal(AcceptedAppendResult.ConflictError, result.Error);
    }

    [Fact]
    public void ShouldTreatOnlyNamedErrorsAsTerminalNoOpsGivenAFailedPreparation()
    {
        // Arrange
        var invalid = new RequestError(RequestErrorKind.Validation, "Invalid.");

        // Act
        var named = UnitResponses.ToResult(Result<AcceptedAppendResult>.Failure(NoOp), NoOp);
        var unnamed = UnitResponses.ToResult(Result<AcceptedAppendResult>.Failure(NoOp));
        var other = UnitResponses.ToResult(Result<AcceptedAppendResult>.Failure(invalid), NoOp);

        // Assert
        Assert.True(named.IsSuccess);
        Assert.Equal(NoOp, unnamed.Error);
        Assert.Equal(RequestErrorKind.Validation, other.Error?.Kind);
    }

    [Fact]
    public void ShouldMakeTheOwnerItsOwnFirstAffectedAndRevisionScopeGivenARootOperation()
    {
        // Arrange
        var role = new Role(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var commandId = Uuid.CreateVersion4();

        // Act
        var operation = OwnerProvenance.For(role, commandId, "DeleteRole", Actor, "current", 7, new string('a', 64),
            DateTimeOffset.UnixEpoch);

        // Assert
        Assert.Equal(commandId, operation.LogicalId);
        Assert.Equal(commandId, operation.RootOperationId);
        Assert.Equal(role.Stream, operation.Owner);
        Assert.Equal(role.Stream, operation.AffectedOwner);
        Assert.Equal(role.Stream, operation.ExpectedRevisionOwner);
        Assert.Equal(7UL, operation.OriginalExpectedRevision);
    }

    [Fact]
    public void ShouldFailClosedGivenAFactThatAcceptsAForeignOperation()
    {
        // Arrange
        var role = new Role(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var foreign = OwnerProvenance.For(new Role(Uuid.CreateVersion4(), Uuid.CreateVersion4()), Uuid.CreateVersion4(),
            "DeleteRole", Actor, "current", null, new string('a', 64), DateTimeOffset.UnixEpoch);

        // Act
        var wrongOwner = Assert.Throws<InvalidOperationException>(() =>
            OwnerProvenance.RequireOwn(role, foreign.LogicalId, foreign));
        var wrongId = Assert.Throws<InvalidOperationException>(() =>
            OwnerProvenance.RequireOwn(role, Uuid.CreateVersion4(),
                OwnerProvenance.For(role, Uuid.CreateVersion4(), "DeleteRole", Actor, "current", null,
                    new string('a', 64), DateTimeOffset.UnixEpoch)));

        // Assert
        Assert.Contains("Role", wrongOwner.Message, StringComparison.Ordinal);
        Assert.Contains("Role", wrongId.Message, StringComparison.Ordinal);
    }
}
