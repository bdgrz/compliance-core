using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ExaminationSignOffAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.ExaminationSignOff;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ExaminationSignOffTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid SignOffId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid CEOId = Uuid.CreateVersion4();
    static readonly Uuid CISOId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldBindManagementApprovalToExactInputsAndAcknowledgeEveryOpenItemGivenOutcome()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();

        // Act
        var missingAcknowledgement = signOff.SignOff(content with
        {
            AcknowledgedMaterialItemIds = [],
        }, CEOId, "CEO", "Chief Executive", Now);
        var signed = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingAcknowledgement).Code);
        Assert.Null(signed);
        Assert.Equal(content.PackageManifestSha256, signOff.Current!.Content.PackageManifestSha256);
        Assert.Equal(content.AssertionContentSha256, signOff.Current!.Content.AssertionContentSha256);
        Assert.Equal("approved", signOff.Current!.Content.Decision);
        Assert.Equal(CEOId, signOff.Current!.Content.Approvers.Single().MemberId);
        Assert.Equal(content.UnresolvedMaterialItemIds.Order(),
            signOff.Current!.Content.AcknowledgedMaterialItemIds.Order());
    }

    [Fact]
    public void ShouldRequireAuthorizedManagementSignerGivenCompleteExternalOutcome()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);

        // Act
        var failure = signOff.SignOff(Content(), Uuid.CreateVersion4(), "Contributor", "Contributor", Now);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, signOff.Revision);
    }

    [Fact]
    public void ShouldRetainAttributedFinalCommitmentsAndDueDatesGivenTypeIISignOff()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var commitment = new ExaminationCommitment(Uuid.CreateVersion4(),
            "Complete the access review evidence remediation.", CISOId,
            new DateOnly(2027, 2, 15), "exception", Uuid.CreateVersion4(), 3,
            new string('f', 64));
        var content = Content() with { ScopeKind = "type_ii", Commitments = [commitment] };

        // Act
        var signed = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Null(signed);
        Assert.Equal("type_ii", signOff.Current!.Content.ScopeKind);
        Assert.Equal(commitment, signOff.Current.Content.Commitments.Single());
    }

    [Fact]
    public void ShouldReplayImmutableManagementDecisionGivenSignOffEvent()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        Assert.Null(signOff.SignOff(Content(), CEOId, "CEO", "Chief Executive", Now));
        var events = new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ExaminationSignOffAggregate>(
            new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId)).Given(events).Aggregate;

        // Assert
        Assert.Equal("approved", replay.Current!.Status);
        Assert.Equal(signOff.Current!.Content.PackageManifestSha256,
            replay.Current.Content.PackageManifestSha256);
        Assert.Equal(signOff.Current.Content.KnownExceptions.ToArray(),
            replay.Current.Content.KnownExceptions.ToArray());
        Assert.Equal(signOff.Current.Content.UnresolvedMaterialItemIds.ToArray(),
            replay.Current.Content.UnresolvedMaterialItemIds.ToArray());
        Assert.Equal(CEOId, replay.Current.SignedOffByMemberId);
    }

    [Fact]
    public void ShouldAcknowledgeExactRetryWithoutNewFactGivenSignOffAlreadyRecordedWithDifferentRoleCase()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();
        Assert.Null(signOff.SignOff(content, CEOId, "ceo", "Chief Executive", Now));

        // Act
        var retry = signOff.SignOff(content, CEOId, "ceo", "Chief Executive", Now);
        var retryExactCase = signOff.SignOff(content, CEOId, "CEO", " Chief Executive ", Now);

        // Assert
        Assert.Null(retry);
        Assert.Null(retryExactCase);
        Assert.Equal(1, signOff.Revision);
        Assert.Single(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangedIntentGivenSignOffAlreadyRecorded()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();
        Assert.Null(signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now));

        // Act
        var changedDecision = signOff.SignOff(content with { Decision = "rejected" },
            CEOId, "CEO", "Chief Executive", Now);
        var changedTime = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now.AddMinutes(1));
        var changedDisplay = signOff.SignOff(content, CEOId, "CEO", "Someone Else", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedDecision).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedTime).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changedDisplay).Code);
        Assert.Equal("approved", signOff.Current!.Status);
        Assert.Single(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNoFactGivenAcknowledgementThatDiffersFromUnresolvedItems()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();

        // Act
        var extra = signOff.SignOff(content with
        {
            AcknowledgedMaterialItemIds = [.. content.AcknowledgedMaterialItemIds, Uuid.CreateVersion4()],
        }, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(extra).Code);
        Assert.Null(signOff.Current);
        Assert.Empty(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNoFactGivenSignerRoleThatDiffersFromNamedApprover()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);

        // Act
        var failure = signOff.SignOff(Content(), CEOId, "CISO", "Chief Executive", Now);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(signOff.Current);
        Assert.Empty(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNoFactGivenInvalidHashOrMissingAttribution()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();

        // Act
        var badHash = signOff.SignOff(content with { PackageManifestSha256 = "short" },
            CEOId, "CEO", "Chief Executive", Now);
        var noDisplay = signOff.SignOff(content, CEOId, "CEO", " ", Now);
        var noTime = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", default);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(badHash).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(noDisplay).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(noTime).Code);
        Assert.Empty(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldRetryExactlyAndRefuseChangedIntentGivenReplayedSignOff()
    {
        // Arrange
        var content = Content();
        var original = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        Assert.Null(original.SignOff(content, CEOId, "CEO", "Chief Executive", Now));
        var events = new AggregateScenario<ExaminationSignOffAggregate>(original).PendingEvents.ToArray();
        var replay = new AggregateScenario<ExaminationSignOffAggregate>(
            new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId)).Given(events).Aggregate;

        // Act
        var retry = replay.SignOff(content, CEOId, "CEO", "Chief Executive", Now);
        var changed = replay.SignOff(content with { Comments = "Different comments." },
            CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(1, replay.Revision);
    }

    [Fact]
    public void ShouldAppendNoFactGivenAcknowledgementOfDifferentItemsWithSameCount()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();

        // Act
        var failure = signOff.SignOff(content with
        {
            AcknowledgedMaterialItemIds = [Uuid.CreateVersion4()],
        }, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Null(signOff.Current);
        Assert.Empty(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldAcknowledgeExactRetryWithoutNewFactGivenAcknowledgementsInDifferentOrder()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var content = Content() with
        {
            UnresolvedMaterialItemIds = [first, second],
            AcknowledgedMaterialItemIds = [first, second],
        };
        Assert.Null(signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now));

        // Act
        var retry = signOff.SignOff(content with
        {
            UnresolvedMaterialItemIds = [second, first],
            AcknowledgedMaterialItemIds = [second, first],
        }, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Null(retry);
        Assert.Single(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSecondApproverGivenSignOffAlreadyRecordedByOtherApprover()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content() with { Approvers = [new(CEOId, "CEO"), new(CISOId, "CISO")] };
        Assert.Null(signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now));

        // Act
        var competing = signOff.SignOff(content, CISOId, "CISO", "Chief Security", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(competing).Code);
        Assert.Equal(CEOId, signOff.Current!.SignedOffByMemberId);
        Assert.Single(new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents);
    }

    static ExaminationSignOffContent Content()
    {
        var unresolvedItemId = Uuid.CreateVersion4();
        return new ExaminationSignOffContent(ExaminationId, "type_i",
            Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), new string('b', 64), Uuid.CreateVersion4(), 1,
            new string('c', 64), Uuid.CreateVersion4(), new string('d', 64), Uuid.CreateVersion4(),
            new string('e', 64), [new("finding", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
                new string('f', 64))], [unresolvedItemId],
            [unresolvedItemId], "approved",
            "Reviewed the external report, exact package, assertion, and open item.",
            [new(CEOId, "CEO")]);
    }
}
