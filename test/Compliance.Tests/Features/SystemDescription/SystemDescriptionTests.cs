using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid DescriptionId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid BaselineId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectApprovalGivenIncompleteOrUnjustifiedSections()
    {
        // Arrange
        var incomplete = NewDescription(Content() with
        {
            Sections = WithSection(Content().Sections, "people", "incomplete", "Roster pending", null),
        });
        ReviewCurrent(incomplete);
        var notApplicable = new SystemDescriptionAggregate(TenantId, ProgramId, DescriptionId);

        // Act
        var incompleteApproval = incomplete.Approve(1, ApproverId, "Approver",
            "Reviewed complete description", Now.AddDays(2));
        var invalidNotApplicable = notApplicable.Create(Uuid.CreateVersion4(), TypeIScope(),
            Content() with
            {
                Sections = WithSection(Content().Sections, "subservice_organizations",
                    "not_applicable", "", null),
            }, AuthorId, "Author", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(incompleteApproval).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(invalidNotApplicable).Code);
        Assert.Empty(new AggregateScenario<SystemDescriptionAggregate>(notApplicable).PendingEvents);
    }

    [Fact]
    public void ShouldRequireIndependentExactVersionReviewAndProtectApprovedSuccessorGivenDraft()
    {
        // Arrange
        var description = NewDescription(Content());
        var original = description.CurrentVersion!;

        // Act
        var authorReview = description.Review(original.Version, AuthorId, "Author", "approve",
            "Self review", Now.AddDays(1));
        var review = description.Review(original.Version, ReviewerId, "Reviewer", "approve",
            "Reviewed against the baseline", Now.AddDays(1));
        var authorApproval = description.Approve(original.Version, AuthorId, "Author",
            "Self approval", Now.AddDays(2));
        var approval = description.Approve(original.Version, ApproverId, "Approver",
            "Management approved", Now.AddDays(2));
        var bound = description.BindToExamination(original.Version, ExaminationId,
            "type_i_baseline", BaselineId, ApproverId, "Approver", Now.AddDays(3));
        var wrongBinding = description.BindToExamination(original.Version, ExaminationId,
            "type_i_baseline", Uuid.CreateVersion4(), ApproverId, "Approver", Now.AddDays(4));
        var successor = description.ProposeSuccessor(description.Revision,
            Uuid.CreateVersion4(), Content() with { Title = "System Description v2" },
            "Reflect an approved scope change", AuthorId, "Author", Now.AddDays(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(authorReview).Code);
        Assert.Null(review);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(authorApproval).Code);
        Assert.Null(approval);
        Assert.Null(bound);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(wrongBinding).Code);
        Assert.Null(successor);
        Assert.Equal("approved", description.FindVersion(original.Version)!.Status);
        Assert.Equal("System Description v2", description.CurrentVersion!.Content.Title);
        Assert.NotEqual(original.SystemDescriptionVersionId,
            description.CurrentVersion.SystemDescriptionVersionId);
        Assert.Equal("System Description", original.Content.Title);
    }

    [Fact]
    public void ShouldBlockStaleMissingOrContradictorySourcesGivenApproval()
    {
        // Arrange
        var stale = NewDescription(Content() with
        {
            Sources = [Source("stale")],
        });
        ReviewCurrent(stale);
        var missing = NewDescription(Content() with
        {
            Sources = [Source("missing")],
        });
        ReviewCurrent(missing);
        var contradictory = NewDescription(Content() with
        {
            Sources = [Source("contradictory")],
        });
        ReviewCurrent(contradictory);

        // Act
        var staleApproval = stale.Approve(1, ApproverId, "Approver", "Ready",
            Now.AddDays(2));
        var missingApproval = missing.Approve(1, ApproverId, "Approver", "Ready",
            Now.AddDays(2));
        var contradictoryApproval = contradictory.Approve(1, ApproverId, "Approver", "Ready",
            Now.AddDays(2));

        // Assert
        Assert.All([staleApproval, missingApproval, contradictoryApproval], failure =>
            Assert.Equal(CommandFailureCode.StateConflict,
                Assert.IsType<CommandFailure>(failure).Code));
        Assert.All([stale, missing, contradictory], description =>
            Assert.Null(description.FindApproval(description.CurrentVersion!.SystemDescriptionVersionId)));
    }

    [Fact]
    public void ShouldRequirePeriodChangeDispositionGivenTypeIIApproval()
    {
        // Arrange
        var periodId = Uuid.CreateVersion4();
        var closeSnapshotId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var scope = new SystemDescriptionScope("type_ii", ExaminationId, null, periodId,
            closeSnapshotId);
        var content = Content() with
        {
            PeriodChanges = [new SystemDescriptionPeriodChange(changeId, false,
                new string('c', 64))],
        };
        var description = NewDescription(content, scope);
        ReviewCurrent(description);

        // Act
        var blocked = description.Approve(1, ApproverId, "Approver", "Ready",
            Now.AddDays(2));
        var decision = description.RecordNoChangeDecision(1, changeId,
            "The source change does not affect the described system", ReviewerId, "Reviewer",
            Now.AddDays(2));
        var approved = description.Approve(1, ApproverId, "Approver", "Ready",
            Now.AddDays(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(blocked).Code);
        Assert.Null(decision);
        Assert.Null(approved);
        Assert.Single(description.NoChangeDecisions);
        Assert.Equal(closeSnapshotId, description.FindApproval(
            description.CurrentVersion!.SystemDescriptionVersionId)!.CloseSnapshotId);
    }

    [Fact]
    public void ShouldAdvanceSourceBindingAsSuccessorVersionGivenRefresh()
    {
        // Arrange
        var description = NewDescription(Content());
        var original = description.CurrentVersion!;

        // Act
        var staleRevision = description.BindSources(0, Uuid.CreateVersion4(), [Source("current")], AuthorId,
            "Author", Now.AddDays(1));
        var refreshed = description.BindSources(1, Uuid.CreateVersion4(), [Source("current")], AuthorId,
            "Author", Now.AddDays(2));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(staleRevision).Code);
        Assert.Null(refreshed);
        Assert.Equal("superseded", description.FindVersion(original.Version)!.Status);
        Assert.Empty(original.Content.Sources);
        Assert.Single(description.CurrentVersion!.Content.Sources);
        Assert.Equal(2, description.CurrentVersion.Version);
        Assert.Collection(new AggregateScenario<SystemDescriptionAggregate>(description).PendingEvents,
            created => Assert.IsType<SystemDescriptionDraftCreated>(created),
            bound => Assert.IsType<SystemDescriptionSourcesBound>(bound));
    }

    static SystemDescriptionAggregate NewDescription(SystemDescriptionDraftContent content,
        SystemDescriptionScope? scope = null)
    {
        var description = new SystemDescriptionAggregate(TenantId, ProgramId, DescriptionId);
        Assert.Null(description.Create(Uuid.CreateVersion4(), scope ?? TypeIScope(), content,
            AuthorId, "Author", Now));
        return description;
    }

    static void ReviewCurrent(SystemDescriptionAggregate description) =>
        Assert.Null(description.Review(description.CurrentVersion!.Version, ReviewerId,
            "Reviewer", "approve", "Reviewed exact version", Now.AddDays(1)));

    static SystemDescriptionScope TypeIScope() =>
        new("type_i", ExaminationId, BaselineId, null, null);

    static SystemDescriptionDraftContent Content() => new("System Description",
        [
            new("services", "Services", "Hosted service", "complete", null),
            new("commitments_requirements", "Commitments and requirements", "Availability commitments", "complete", null),
            new("components_infrastructure_software", "Components and infrastructure", "Cloud platform", "complete", null),
            new("people", "People", "Security and operations teams", "complete", null),
            new("procedures", "Procedures", "Change and incident procedures", "complete", null),
            new("information_data_flows", "Information and data flows", "Customer data flows", "complete", null),
            new("system_boundary", "System boundary", "Production system", "complete", null),
            new("control_environment", "Control environment", "Approved controls", "complete", null),
            new("user_entity_controls", "User entity controls", "", "not_applicable", "No user entity controls apply"),
            new("subservice_organizations", "Subservice organizations", "Cloud provider", "complete", null),
            new("exclusions", "Exclusions", "Nonproduction sandbox excluded", "complete", null),
            new("relevant_changes", "Relevant changes", "", "not_applicable", "No changes in this period"),
        ], [], [], []);

    static SystemDescriptionSection[] WithSection(
        IReadOnlyList<SystemDescriptionSection> sections, string id, string status,
        string content, string? rationale) => sections.Select(section => section.SectionId == id
        ? section with { Status = status, Content = content, NotApplicableRationale = rationale }
        : section).ToArray();

    static SystemDescriptionSourceReference Source(string status) =>
        new("control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2,
            new string('b', 64), status);
}
