using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>Owner-level decisions for one immutable client independence preview.</summary>
public sealed class ClientIndependenceDecisionTests
{
    [Fact]
    public void ShouldMaterializeOnePreviewGivenValidAdmittedChild()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var owner = new ClientIndependenceDecision(tenant, evaluation);
        var scenario = new AggregateScenario<ClientIndependenceDecision>(owner);
        var operation = Child(owner, tenant, evaluation, Uuid.CreateVersion4());
        var content = NativeFacts.Ref();
        var intent = NativeFacts.Ref();
        var receipt = NativeFacts.Ref();

        // Act
        var result = owner.Materialize(operation, content, intent, receipt);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<ClientIndependenceEvaluated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(tenant, fact.TenantId);
        Assert.Equal(evaluation, fact.OperationId);
        Assert.Equal(content, fact.EvaluationContent);
        Assert.Equal(intent, fact.Intent);
        Assert.Equal(receipt, fact.OperationResult);
        Assert.Equal(1UL, fact.Revision);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ShouldRefusePreviewWithoutFactGivenEmptyOwnerIdentity(bool emptyTenant, bool emptyEvaluation)
    {
        // Arrange
        var tenant = emptyTenant ? Uuid.Empty : Uuid.CreateVersion4();
        var evaluation = emptyEvaluation ? Uuid.Empty : Uuid.CreateVersion4();
        var owner = new ClientIndependenceDecision(tenant, evaluation);
        var scenario = new AggregateScenario<ClientIndependenceDecision>(owner);
        var operation = Child(owner, tenant, evaluation, Uuid.CreateVersion4());

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Evaluated);
    }

    [Theory]
    [InlineData("logical")]
    [InlineData("root")]
    [InlineData("cause")]
    [InlineData("kind")]
    [InlineData("owner")]
    [InlineData("source")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    public void ShouldRefusePreviewWithoutFactGivenInvalidChildProvenance(string defect)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var owner = new ClientIndependenceDecision(tenant, evaluation);
        var scenario = new AggregateScenario<ClientIndependenceDecision>(owner);
        var operation = Child(owner, tenant, evaluation, Uuid.CreateVersion4());
        var foreign = new ClientIndependenceDecision(Uuid.CreateVersion4(), evaluation).Stream;
        operation = defect switch
        {
            "logical" => operation with { LogicalId = Uuid.Empty },
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "cause" => operation with { CausationEventId = null },
            "kind" => operation with { CommandKind = "Other" },
            "owner" => operation with { Owner = foreign },
            "source" => operation with { FirstOwner = foreign },
            "affected" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Evaluated);
    }

    [Fact]
    public void ShouldRecognizeOriginalAndRefuseCompetingPreviewGivenValidReplayedHistory()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var logical = Uuid.CreateVersion4();
        var seed = new ClientIndependenceDecision(tenant, evaluation);
        var operation = Child(seed, tenant, evaluation, logical);
        var content = NativeFacts.Ref();
        Assert.True(seed.Materialize(operation, content, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        var owner = NativeFacts.Replay(new ClientIndependenceDecision(tenant, evaluation, logical), NativeFacts.Raised(seed));
        var scenario = new AggregateScenario<ClientIndependenceDecision>(owner);
        var changed = operation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var retry = owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
        var changedRetry = owner.Acceptance.Decide(AcceptedOperationBinding.Of(changed));
        var competing = owner.Materialize(Child(owner, tenant, evaluation, Uuid.CreateVersion4()),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, retry);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changedRetry);
        Assert.False(competing.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.Equal(content, owner.Evaluated!.EvaluationContent);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepDecisionIdentityDistinctGivenTenantAndEvaluation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var first = new ClientIndependenceDecision(tenant, evaluation);

        // Act
        var retry = new ClientIndependenceDecision(tenant, evaluation, Uuid.CreateVersion4());
        var anotherTenant = new ClientIndependenceDecision(Uuid.CreateVersion4(), evaluation);
        var anotherEvaluation = new ClientIndependenceDecision(tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, retry.Stream);
        Assert.Equal(first.Id, retry.Id);
        Assert.NotEqual(first.Stream, anotherTenant.Stream);
        Assert.NotEqual(first.Stream, anotherEvaluation.Stream);
    }

    static OperationProvenance Child(ClientIndependenceDecision owner, Uuid tenant, Uuid evaluation, Uuid logical) =>
        new(logical, evaluation, Uuid.CreateVersion4(), "MaterializeClientIndependencePreview",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Member"), owner.Stream, "initial", 0,
            new string('b', 64), NativeFacts.Now, owner.Stream, owner.Stream,
            new ClientServiceIndependence(tenant).Stream);
}
