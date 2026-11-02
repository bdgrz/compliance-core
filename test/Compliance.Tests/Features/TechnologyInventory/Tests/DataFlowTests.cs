using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class DataFlowTests
{
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordRevisionOneWithNormalizedActiveContentGivenValidCreation()
    {
        // Arrange
        var flow = NewFlow();

        // Act
        var result = Record(flow, Uuid.CreateVersion4(),
            Content() with { Purpose = "  Payroll export  ", Lifecycle = "retired" });

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<DataFlowRevisionRecorded>(Assert.Single(Facts(flow)));
        Assert.Equal(1, fact.Revision);
        Assert.Equal("Payroll export", fact.Content.Purpose);
        Assert.Equal("active", fact.Content.Lifecycle);
        Assert.Equal(1, flow.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidCreation()
    {
        // Arrange
        var flow = NewFlow();

        // Act
        var result = Record(flow, Uuid.CreateVersion4(), Content() with { InformationAssetIds = [] });

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(flow));
        Assert.False(flow.IsCreated);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenExistingFlow()
    {
        // Arrange
        var flow = NewFlow();
        Assert.True(Record(flow, Uuid.CreateVersion4(), Content()).IsSuccess);

        // Act
        var result = Record(flow, Uuid.CreateVersion4(), Content() with { Purpose = "Other" });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(flow));
        Assert.Equal(1, flow.Revision);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenForeignInventoryStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var foreign = InventoryFacts.ComponentFact(tenantId, id, 1,
            new TechnologyComponentContent("network", "VPC", OwnerId, "production", "us-east-1", null, null, null,
                "active"), Author, Now);
        var flow = new AggregateScenario<DataFlow>(new DataFlow(tenantId, id))
            .Given(DomainEventSeed.Attach(foreign, id, 1)).Aggregate;

        // Act
        var result = Record(flow, Uuid.CreateVersion4(), Content());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(Facts(flow));
        Assert.False(flow.IsCreated);
        Assert.Null(flow.Content);
    }

    [Fact]
    public void ShouldRefuseOperationAndAppendNothingGivenAnotherOwnerStream()
    {
        // Arrange
        var flow = NewFlow();
        var other = NewFlow();
        var operation = Operation(other, Uuid.CreateVersion4(), "RecordDataFlow", null);

        // Act
        var result = flow.Record(operation, NativeFacts.Ref("first-response-descriptor.v1"), Content(), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(flow));
    }

    [Fact]
    public void ShouldRefuseOperationAndAppendNothingGivenEmptyLogicalId()
    {
        // Arrange
        var flow = NewFlow();

        // Act
        var result = Record(flow, Uuid.Empty, Content());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(flow));
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenUnrecordedFlow()
    {
        // Arrange
        var flow = NewFlow();

        // Act
        var result = Revise(flow, Uuid.CreateVersion4(), 0, current => current);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Facts(flow));
    }

    [Fact]
    public void ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedFlow()
    {
        // Arrange
        var flow = NewFlow();
        Assert.True(Record(flow, Uuid.CreateVersion4(), Content()).IsSuccess);
        Assert.True(Revise(flow, Uuid.CreateVersion4(), 1, current => current with { Purpose = "Second" }).IsSuccess);

        // Act
        var result = Revise(flow, Uuid.CreateVersion4(), 1, current => current with { Purpose = "Competing" });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, Facts(flow).Length);
        Assert.Equal(2, flow.Revision);
        Assert.Equal("Second", flow.Content!.Purpose);
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenEarlierEffectiveDate()
    {
        // Arrange
        var flow = NewFlow();
        Assert.True(Record(flow, Uuid.CreateVersion4(), Content()).IsSuccess);

        // Act
        var result = Revise(flow, Uuid.CreateVersion4(), 1,
            current => current with { EffectiveFrom = current.EffectiveFrom.AddDays(-1) });

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Single(Facts(flow));
        Assert.Equal(1, flow.Revision);
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenUnprotectedSensitiveFlow()
    {
        // Arrange
        var flow = NewFlow();
        Assert.True(Record(flow, Uuid.CreateVersion4(), Content() with { Classification = "restricted" }).IsSuccess);

        // Act
        var result = Revise(flow, Uuid.CreateVersion4(), 1,
            current => current with { EncryptedAtRest = false, ExceptionReference = null });

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Single(Facts(flow));
        Assert.True(flow.Content!.EncryptedAtRest);
    }

    [Fact]
    public void ShouldCarryEveryAssetInOrderGivenLargeAssetListReplay()
    {
        // Arrange
        var source = NewFlow();
        var assets = Enumerable.Range(0, 150).Select(_ => Uuid.CreateVersion4()).ToArray();
        Assert.True(Record(source, Uuid.CreateVersion4(), Content() with { InformationAssetIds = assets }).IsSuccess);

        // Act
        var replayed = NativeFacts.Replay(new DataFlow(TenantOf(source), source.Id), NativeFacts.Raised(source));

        // Assert
        Assert.Equal(assets, replayed.Content!.InformationAssetIds);
    }

    [Fact]
    public void ShouldRestoreRevisionAndContentGivenValidHistoryReplay()
    {
        // Arrange
        var source = NewFlow();
        Assert.True(Record(source, Uuid.CreateVersion4(), Content()).IsSuccess);
        Assert.True(Revise(source, Uuid.CreateVersion4(), 1, current => current with { Purpose = "Second" }).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new DataFlow(TenantOf(source), source.Id), history);
        var restored = replayed.Content!;
        var stale = Revise(replayed, Uuid.CreateVersion4(), 1, current => current);
        var next = Revise(replayed, Uuid.CreateVersion4(), 2, current => current with { Purpose = "Third" });

        // Assert
        Assert.True(replayed.IsCreated);
        Assert.True(TechnologyInventoryRules.SameContent(source.Content!, restored));
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Equal(3, replayed.Revision);
    }

    [Fact]
    public void ShouldResolveOriginalAcceptanceGivenReplayAfterLaterFacts()
    {
        // Arrange
        var source = NewFlow();
        var root = Uuid.CreateVersion4();
        Assert.True(Record(source, root, Content()).IsSuccess);
        Assert.True(Revise(source, Uuid.CreateVersion4(), 1, current => current with { Purpose = "Second" }).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new DataFlow(TenantOf(source), source.Id, root), history);
        var decision = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(replayed.Acceptance.SelectedProvenance!));

        // Assert
        var original = Assert.IsType<DataFlowRevisionRecorded>(replayed.Acceptance.SelectedFact);
        Assert.Equal(1, original.Revision);
        Assert.Equal("RecordDataFlow", replayed.Acceptance.SelectedProvenance?.CommandKind);
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Equal(2, replayed.Revision);
    }

    static DataFlow NewFlow() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static Uuid TenantOf(DataFlow flow) => Assert.IsType<DataFlowRevisionRecorded>(Facts(flow)[0]).TenantId;

    static DomainEvent[] Facts(DataFlow flow) => new AggregateScenario<DataFlow>(flow).PendingEvents.ToArray();

    static Result Record(DataFlow flow, Uuid logicalId, DataFlowContent content) =>
        flow.Record(Operation(flow, logicalId, "RecordDataFlow", null), NativeFacts.Ref("first-response-descriptor.v1"),
            content, Author, Now);

    static Result Revise(DataFlow flow, Uuid logicalId, long expected, Func<DataFlowContent, DataFlowContent> change) =>
        flow.Revise(Operation(flow, logicalId, "ReviseDataFlow", (ulong)expected),
            NativeFacts.Ref("first-response-descriptor.v1"), expected, change, Author, Now);

    static OperationProvenance Operation(DataFlow flow, Uuid logicalId, string commandKind, ulong? expected) =>
        new(logicalId, logicalId, null, commandKind, Author, flow.Stream, expected is null ? "initial" : "current",
            expected, new string('a', 64), Now, flow.Stream, flow.Stream);

    static DataFlowContent Content() =>
        new("technology_component", Uuid.CreateVersion4(), "external_party", null, "Payroll Co",
            [Uuid.CreateVersion4()], "Payroll export", true, true, null, new DateOnly(2026, 10, 1), OwnerId, "active",
            "internal");
}
