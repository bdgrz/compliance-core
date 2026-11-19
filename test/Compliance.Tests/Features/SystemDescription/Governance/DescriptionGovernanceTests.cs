using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Governance.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Governance.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Governance.Events;
using Bdgrz.Compliance.Features.SystemDescription.Governance.Models;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Governance;

public sealed class DescriptionGovernanceTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly ActorReference Reviewer = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 11, 4);

    [Fact]
    public void ShouldPermitOneOpenDraftPerProgramGivenCompetingDraftIdentities()
    {
        // Arrange
        var line = new SystemDescriptionLine(Tenant, Program);
        var first = new StartSystemDescriptionDraft(Uuid.CreateVersion4(), Tenant, Program, Uuid.CreateVersion4(), 0);
        Assert.Null(line.Start(first, Author, Now));
        var before = new AggregateScenario<SystemDescriptionLine>(line).PendingEvents.ToArray();

        // Act
        var competing = line.Start(first with { CommandId = Uuid.CreateVersion4(), DraftId = Uuid.CreateVersion4() }, Author, Now);
        var retry = line.Start(first, Author, Now.AddDays(1));

        // Assert
        Assert.NotNull(competing);
        Assert.Null(retry);
        Assert.Equal(first.DraftId, line.OpenDraftId);
        Assert.Equal(before, new AggregateScenario<SystemDescriptionLine>(line).PendingEvents);
    }

    [Fact]
    public void ShouldPublishOneImmutableVersionGivenIndependentReviewOfAllSections()
    {
        // Arrange
        var line = new SystemDescriptionLine(Tenant, Program);
        var start = new StartSystemDescriptionDraft(Uuid.CreateVersion4(), Tenant, Program, Uuid.CreateVersion4(), 0);
        Assert.Null(line.Start(start, Author, Now));
        var opened = Assert.IsType<SystemDescriptionDraftStarted>(new AggregateScenario<SystemDescriptionLine>(line).PendingEvents.Single());
        var draft = new SystemDescriptionDraft(Tenant, Program, start.DraftId);
        Assert.Null(draft.Open(opened));
        var sections = DescriptionSections.Names.Select(name => new DescriptionSection(name, "Management description", [])).ToArray();
        Assert.Null(draft.Revise(new(Uuid.CreateVersion4(), Tenant, Program, start.DraftId, 1, Today, sections), Author, Now));
        var review = new ReviewSystemDescriptionDraft(Uuid.CreateVersion4(), Tenant, Program, start.DraftId, 2,
            "approve", "All sections reviewed");

        // Act
        var self = draft.Review(review, Author, null, Now.AddMinutes(1));
        var approve = draft.Review(review, Reviewer, null, Now.AddMinutes(1));
        var approved = Assert.IsType<SystemDescriptionDraftApproved>(new AggregateScenario<SystemDescriptionDraft>(draft).PendingEvents.Last());
        var record = line.Record(approved);
        var repeat = line.Record(approved);
        var terminal = draft.Revise(new(Uuid.CreateVersion4(), Tenant, Program, start.DraftId, 3, Today,
            [new("services", "Changed", [])]), Reviewer, Now.AddMinutes(2));

        // Assert
        Assert.NotNull(self);
        Assert.Null(approve);
        Assert.Null(record);
        Assert.Null(repeat);
        Assert.NotNull(terminal);
        Assert.Null(line.OpenDraftId);
        Assert.Equal(9, line.CurrentVersion!.Sections.Count);
        Assert.Equal(9, line.CurrentVersion.ChangedSections.Count);
        Assert.Equal(2, line.Revision);
    }

    [Fact]
    public void ShouldResolveOnlyByLaterChangedSectionGivenOpenReview()
    {
        // Arrange
        var item = Uuid.CreateVersion4();
        var review = new SectionReview(Tenant, Program, "services", "change", item);
        Assert.Null(review.Open(1, Uuid.Parse(Author.Id, System.Globalization.CultureInfo.InvariantCulture), [], Now));
        var version = new ApprovedDescription(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, Today,
            [new("services", "Current description", [])], ["incidents"], Uuid.Parse(Reviewer.Id,
                System.Globalization.CultureInfo.InvariantCulture), Now.AddMinutes(1), null);

        // Act
        var unchanged = review.ResolveByVersion(version);
        var tooEarly = review.ResolveByVersion(version with { ChangedSections = ["services"], ApprovedAt = Now });
        var later = review.ResolveByVersion(version with { ChangedSections = ["services"] });
        var repeat = review.ResolveByVersion(version with { ChangedSections = ["services"] });

        // Assert
        Assert.Null(unchanged);
        Assert.Null(tooEarly);
        Assert.Null(later);
        Assert.Null(repeat);
        Assert.Equal("resolved", review.Status);
        Assert.Equal(2, review.Revision);
    }

    [Fact]
    public void ShouldRequireIndependentNoChangeAndLaterAssessmentToReopenGivenResolvedReview()
    {
        // Arrange
        var item = Uuid.CreateVersion4();
        var review = new SectionReview(Tenant, Program, "incidents", "incident", item);
        var assessor = Uuid.Parse(Author.Id, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Null(review.Open(1, assessor, [], Now));
        var command = new RecordSectionNoChange(Uuid.CreateVersion4(), Tenant, Program, "incidents", "incident", item, 1,
            "The approved description already covers this event");

        // Act
        var self = review.NoChange(command, Author, null, Now.AddMinutes(1));
        var independent = review.NoChange(command, Reviewer, null, Now.AddMinutes(1));
        var old = review.Open(1, assessor, [], Now);
        var later = review.Open(3, assessor, [], Now.AddMinutes(2));
        var withdrawn = review.Withdraw(4, "Reassessed not significant", Now.AddMinutes(3));

        // Assert
        Assert.NotNull(self);
        Assert.Null(independent);
        Assert.Null(old);
        Assert.Null(later);
        Assert.Null(withdrawn);
        Assert.Equal("withdrawn", review.Status);
        Assert.Equal(4, review.AssessmentRevision);
        Assert.Equal(4, review.Revision);
    }
}
