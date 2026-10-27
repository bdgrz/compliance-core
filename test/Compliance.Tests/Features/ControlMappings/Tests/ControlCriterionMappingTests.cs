using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>
///     The mapping line of one (program, control, edition, criterion) tuple: one pending proposal, one accepted
///     version, independent review, exact succession and retirement of the version it names only.
/// </summary>
public sealed class ControlCriterionMappingTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = NativeFacts.Now;

    static ControlCriterionMappingRules Line(string criterion = "CC6.1", Uuid? control = null) =>
        new(TenantId, ProgramId, control ?? ControlId, EditionId, criterion);

    static Result Propose(ControlCriterionMappingRules line, Uuid mappingId, long expected, Uuid? controlVersion = null,
        string rationale = "Restricts access") => line.Propose(
        MappingRuleFacts.Root(line, mappingId, "ProposeControlCriterionMapping", expected, AuthorId), controlVersion ?? ControlVersionId,
        "criterion", rationale, "All production systems", AuthorId, "Author", Now);

    static Result Review(ControlCriterionMappingRules line, Uuid mappingId, long expected, string outcome,
        SeparationOfDutiesWaiver? waiver = null, Uuid? reviewer = null) => line.Review(
        MappingRuleFacts.Root(line, Uuid.CreateVersion4(), "ReviewControlCriterionMapping", expected, reviewer ?? ReviewerId),
        mappingId, outcome, "Fits", reviewer ?? ReviewerId, "Reviewer", Now.AddMinutes(2), waiver);

    static Result Retire(ControlCriterionMappingRules line, Uuid mappingId, long expected) => line.Retire(
        MappingRuleFacts.Root(line, Uuid.CreateVersion4(), "RetireControlCriterionMapping", expected, ReviewerId), mappingId,
        "Superseded by redesign", ReviewerId, "Reviewer", Now.AddDays(1));

    static SeparationOfDutiesWaiver Waiver(Uuid mappingId, long revision)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlCriterionMapping,
                mappingId, mappingId, revision, SeparationOfDutiesActions.Review), AuthorId, Uuid.CreateVersion4(),
            "Requester", "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }

    [Fact]
    public void ShouldAllowProposerReviewGivenApprovedWaiverForExactRevision()
    {
        // Arrange
        var line = Line();
        var mappingId = Uuid.CreateVersion4();
        Assert.True(Propose(line, mappingId, 0).IsSuccess);
        var exact = Waiver(mappingId, 1);
        var wrongRevision = Waiver(mappingId, 7);

        // Act
        var denied = Review(line, mappingId, 1, "accept", wrongRevision, AuthorId);
        var waived = Review(line, mappingId, 1, "accept", exact, AuthorId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error!.Kind);
        Assert.True(waived.IsSuccess);
        Assert.Equal("active", line.View!.Status);
        Assert.Equal(exact.Id, line.View.Versions[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectSelfReviewWithoutWaiverAndInvalidReviewGivenUnknownOutcomeStaleRevisionOrOtherProposal()
    {
        // Arrange
        var line = Line();
        var mappingId = Uuid.CreateVersion4();
        Assert.True(Propose(line, mappingId, 0).IsSuccess);

        // Act
        var self = Review(line, mappingId, 1, "accept", null, AuthorId);
        var outcome = Review(line, mappingId, 1, "satisfied");
        var stale = Review(line, mappingId, 0, "accept");
        var other = Review(line, Uuid.CreateVersion4(), 1, "accept");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, self.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, outcome.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, other.Error!.Kind);
        Assert.Equal(1, line.Revision);
    }

    [Fact]
    public void ShouldReplayTheSameLineGivenRoundTrippedFacts()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "accept").IsSuccess);
        Assert.True(Propose(line, second, 2, Uuid.CreateVersion4(), "Narrowed").IsSuccess);
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        Assert.True(Retire(line, second, 4).IsSuccess);
        var facts = MappingRuleFacts.Raised(line);

        // Act
        var replayed = MappingRuleFacts.Replay(Line(), facts);

        // Assert
        Assert.Equal(5, facts.Length);
        Assert.Equal(5, replayed.Revision);
        Assert.Equal(line.View! with { Versions = [] }, replayed.View! with { Versions = [] });
        Assert.Equal(line.View.Versions, replayed.View!.Versions);
        Assert.Equal("retired", replayed.View.Status);
        Assert.Equal(["superseded", "retired"], replayed.View.Versions.Select(static version => version.Status));
        Assert.Equal([first, second], replayed.View.Versions.Select(static version => version.MappingId));
    }

    [Fact]
    public void ShouldSupersedeOnlyTheExactAcceptedMappingAndRefuseOldRetirementGivenSuccessor()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "accept").IsSuccess);
        Assert.True(Propose(line, second, 2, Uuid.CreateVersion4(), "Narrowed").IsSuccess);
        Assert.True(Review(line, second, 3, "accept").IsSuccess);

        // Act
        var old = Retire(line, first, 4);
        var current = Retire(line, second, 4);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, old.Error!.Kind);
        Assert.True(current.IsSuccess);
        Assert.Equal(["superseded", "retired"], line.View!.Versions.Select(static version => version.Status));
    }

    [Fact]
    public void ShouldRejectSecondProposalGivenPendingOrStaleRevisionOrSameAcceptedContent()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);

        // Act
        var competing = Propose(line, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4());
        var stale = Propose(line, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4());
        Assert.True(Review(line, first, 1, "accept").IsSuccess);
        var duplicate = Propose(line, Uuid.CreateVersion4(), 2);
        var retireStale = Retire(line, first, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.Contains("pending", competing.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, retireStale.Error!.Kind);
        Assert.Equal(2, line.Revision);
    }

    [Fact]
    public void ShouldKeepTuplesIndependentGivenDifferentCriteriaAndControls()
    {
        // Arrange
        var access = Line("CC6.1");
        var accounts = Line("CC6.2");
        var otherControl = Line("CC6.1", Uuid.CreateVersion4());

        // Act
        var results = new[]
        {
            Propose(access, Uuid.CreateVersion4(), 0),
            Propose(accounts, Uuid.CreateVersion4(), 0),
            Propose(otherControl, Uuid.CreateVersion4(), 0),
        };

        // Assert
        Assert.All(results, static result => Assert.True(result.IsSuccess));
        Assert.Equal(3, new[] { access.Id, accounts.Id, otherControl.Id }.Distinct().Count());
        Assert.Equal([1L, 1L, 1L], new[] { access.Revision, accounts.Revision, otherControl.Revision });
    }

    [Fact]
    public void ShouldProjectTheSameViewGivenReducerOverRaisedFacts()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var third = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "reject").IsSuccess);
        Assert.True(Propose(line, second, 2, Uuid.CreateVersion4(), "Retry").IsSuccess);
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        Assert.True(Propose(line, third, 4, Uuid.CreateVersion4(), "Remap").IsSuccess);
        Assert.True(Review(line, third, 5, "accept").IsSuccess);
        Assert.True(Retire(line, third, 6).IsSuccess);

        // Act
        ControlCriterionMappingView? projected = null;
        var stages = new List<string>();
        foreach (var fact in MappingRuleFacts.Raised(line))
        {
            projected = ControlCriterionMappingReducer.Apply(projected, fact);
            stages.Add(projected.Status);
        }

        // Assert
        Assert.Equal(line.View! with { Versions = [] }, projected! with { Versions = [] });
        Assert.Equal(line.View.Versions, projected.Versions);
        Assert.Equal(["pending", "rejected", "pending", "active", "pending", "active", "retired"], stages);
        Assert.Equal(["rejected", "superseded", "retired"],
            projected.Versions.Select(static version => version.Status));
    }

    [Fact]
    public void ShouldFailClosedGivenReplayOfAFactThatDoesNotFollowItsLine()
    {
        // Arrange
        var line = Line();
        Assert.True(Propose(line, Uuid.CreateVersion4(), 0).IsSuccess);
        var other = Line("CC6.2");
        Assert.True(Propose(other, Uuid.CreateVersion4(), 0).IsSuccess);

        // Act
        var foreign = MappingRuleFacts.Raised(other);

        // Assert
        Assert.ThrowsAny<Exception>(() => MappingRuleFacts.Replay(Line(), foreign));
    }
}
