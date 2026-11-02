using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PartnerIndependenceDecisionOwnerTests
{
    [Theory]
    [InlineData("tenant")]
    [InlineData("evaluation")]
    [InlineData("logical")]
    [InlineData("root")]
    [InlineData("cause")]
    [InlineData("empty_cause")]
    [InlineData("kind")]
    [InlineData("source")]
    [InlineData("invalid_source")]
    [InlineData("owner")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    public void ShouldRefuseMaterializationWithoutFactGivenInvalidIdentityOrChildProvenance(string defect)
    {
        // Arrange
        var tenant = defect == "tenant" ? Uuid.Empty : Uuid.CreateVersion4();
        var evaluation = defect == "evaluation" ? Uuid.Empty : Uuid.CreateVersion4();
        var owner = new PartnerIndependenceDecision(tenant, evaluation);
        var scenario = new AggregateScenario<PartnerIndependenceDecision>(owner);
        var operation = Child(owner);
        var foreign = new ServiceEngagementIndependence(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "tenant" or "evaluation" => operation,
            "logical" => operation with { LogicalId = Uuid.Empty },
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "cause" => operation with { CausationEventId = null },
            "empty_cause" => operation with { CausationEventId = Uuid.Empty },
            "kind" => operation with { CommandKind = "Other" },
            "source" => operation with { FirstOwner = foreign },
            "invalid_source" => operation with
            {
                FirstOwner = new EventStreamAddress(owner.Stream.Realm, ServiceEngagementIndependence.Area, "not-an-engagement"),
            },
            "owner" => operation with { Owner = foreign },
            "affected" => operation with { AffectedOwner = foreign },
            "revision_owner" => operation with { ExpectedRevisionOwner = foreign },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Recorded);
    }

    [Fact]
    public void ShouldPreserveEvaluationAndRefuseAnotherGivenValidReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var seed = new PartnerIndependenceDecision(tenant, evaluation);
        var content = NativeFacts.Ref();
        var rules = NativeFacts.Ref();
        var intent = NativeFacts.Ref();
        Assert.True(seed.Materialize(Child(seed), content, rules, intent, NativeFacts.Ref()).IsSuccess);
        var facts = NativeFacts.Raised(seed);
        var owner = NativeFacts.Replay(new PartnerIndependenceDecision(tenant, evaluation), facts);
        var scenario = new AggregateScenario<PartnerIndependenceDecision>(owner);

        // Act
        var another = owner.Materialize(Child(owner), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(another.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, another.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        var fact = Assert.IsType<PartnerIndependenceEvaluationRecorded>(Assert.Single(facts));
        Assert.Equal(content, owner.Recorded!.EvaluationContent);
        Assert.Equal(rules, owner.Recorded.RatifiedRulesContent);
        Assert.Equal(intent, owner.Recorded.Intent);
        Assert.Equal(evaluation, fact.Operation.RootOperationId);
    }

    [Fact]
    public void ShouldKeepIdentityStableGivenTenantAndEvaluation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();

        // Act
        var first = new PartnerIndependenceDecision(tenant, evaluation);
        var selected = new PartnerIndependenceDecision(tenant, evaluation, Uuid.CreateVersion4());
        var otherTenant = new PartnerIndependenceDecision(Uuid.CreateVersion4(), evaluation);
        var otherEvaluation = new PartnerIndependenceDecision(tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, selected.Stream);
        Assert.Equal(first.Id, selected.Id);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
        Assert.NotEqual(first.Stream, otherEvaluation.Stream);
    }

    static OperationProvenance Child(PartnerIndependenceDecision owner) =>
        NativeFacts.Root(owner, owner.EvaluationId, nameof(MaterializePartnerIndependenceDecision), 0) with
        {
            LogicalId = Uuid.CreateVersion4(),
            CausationEventId = Uuid.CreateVersion4(),
            FirstOwner = new ServiceEngagementIndependence(owner.TenantId, Uuid.CreateVersion4()).Stream,
        };
}
