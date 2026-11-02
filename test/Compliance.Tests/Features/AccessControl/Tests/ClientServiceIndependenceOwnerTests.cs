using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>Direct decisions of the client service epoch owner.</summary>
public sealed class ClientServiceIndependenceOwnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldAppendOneFactGivenValidRootAtCurrentEpoch(bool evaluation)
    {
        // Arrange
        var owner = new ClientServiceIndependence(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ClientServiceIndependence>(owner);
        var kind = evaluation ? nameof(EvaluateClientIndependence) : nameof(RecordNonattestService);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), kind, 0);
        var content = NativeFacts.Ref();

        // Act
        var result = evaluation
            ? owner.AdmitEvaluation(operation, content, 0, content)
            : owner.RecordService(operation, content, content, content);

        // Assert
        Assert.True(result.IsSuccess);
        if (evaluation)
        {
            Assert.IsType<ClientIndependenceEvaluationAdmitted>(Assert.Single(scenario.PendingEvents));
            Assert.Equal(0UL, owner.Epoch);
        }
        else
        {
            Assert.IsType<NonattestServiceRecorded>(Assert.Single(scenario.PendingEvents));
            Assert.Equal(1UL, owner.Epoch);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseInvalidRootWithoutFactGivenFreshClient(bool evaluation)
    {
        // Arrange
        var owner = new ClientServiceIndependence(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ClientServiceIndependence>(owner);
        var kind = evaluation ? nameof(EvaluateClientIndependence) : nameof(RecordNonattestService);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), kind, 0);
        var foreign = new ClientServiceIndependence(Uuid.CreateVersion4()).Stream;
        var invalid = new[]
        {
            operation with { LogicalId = Uuid.Empty, RootOperationId = Uuid.Empty },
            operation with { RootOperationId = Uuid.CreateVersion4() },
            operation with { Owner = foreign },
            operation with { CommandKind = "OtherCommand" },
            operation with { CausationEventId = Uuid.CreateVersion4() },
            operation with { FirstOwner = foreign },
            operation with { AffectedOwner = foreign },
            operation with { ExpectedRevisionOwner = foreign },
            operation with { Episode = "other" },
            operation with { AuthoredIntentSha256 = "bad" },
            operation with { AcceptedAt = default },
        };
        var content = NativeFacts.Ref();

        // Act
        var results = invalid.Select(value => evaluation
            ? owner.AdmitEvaluation(value, content, 0, content)
            : owner.RecordService(value, content, content, content)).ToArray();

        // Assert
        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        });
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(0UL, owner.Epoch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseWithoutFactGivenEmptyClientIdentity(bool evaluation)
    {
        // Arrange
        var owner = new ClientServiceIndependence(Uuid.Empty);
        var scenario = new AggregateScenario<ClientServiceIndependence>(owner);
        var kind = evaluation ? nameof(EvaluateClientIndependence) : nameof(RecordNonattestService);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), kind, 0);
        var content = NativeFacts.Ref();

        // Act
        var result = evaluation
            ? owner.AdmitEvaluation(operation, content, 0, content)
            : owner.RecordService(operation, content, content, content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }
}
