using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ManagementAssertionAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.ManagementAssertion;
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
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ManagementAssertionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AssertionId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldBindAssertionToExactScopePackageAndAuthoredWordingGivenFrozenTypeIInputs()
    {
        // Arrange
        var content = Content();
        var assertion = new ManagementAssertionAggregate(TenantId, ProgramId, AssertionId);

        // Act
        var created = assertion.Create(content, AuthorId, "CEO", Now);

        // Assert
        Assert.Null(created);
        Assert.Equal(content.PackageManifestId, assertion.Current!.CurrentVersion!.Content.PackageManifestId);
        Assert.Equal(content.PackageManifestSha256,
            assertion.Current.CurrentVersion.Content.PackageManifestSha256);
        Assert.Equal(content.SourceSnapshotId, assertion.Current.CurrentVersion.Content.SourceSnapshotId);
        Assert.Equal("auditor", assertion.Current.CurrentVersion.Content.WordingSource);
        Assert.Equal("Auditor-approved representation wording", assertion.Current.CurrentVersion.Content.AssertionText);
        Assert.Equal("draft", assertion.Current.Status);
    }

    [Fact]
    public void ShouldTreatSameAssertionCreationAsIdempotentGivenLaterRetryTime()
    {
        // Arrange
        var content = Content();
        var assertion = NewAssertion(content);

        // Act
        var retry = assertion.Create(content, AuthorId, "CEO", Now.AddHours(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(1, assertion.Revision);
        Assert.Equal(Now, assertion.Current!.CurrentVersion!.AuthoredAt);
    }

    [Fact]
    public void ShouldRequireIndependentCEOOrCISOSignOffGivenExactAssertionVersion()
    {
        // Arrange
        var assertion = NewAssertion(Content());

        // Act
        var authorApproval = assertion.Approve(1, 1, "approve", "Self approval",
            AuthorId, "CEO", "CEO", Now.AddDays(1));
        var approval = assertion.Approve(1, 1, "approve", "Reviewed exact package and exception set",
            ApproverId, "CISO", "CISO", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(authorApproval).Code);
        Assert.Null(approval);
        Assert.Equal("approved", assertion.Current!.Status);
        Assert.Equal(1, Assert.Single(assertion.Current.Approvals).AssertionVersion);
        Assert.Equal(AuthorId, assertion.Current.Approvals[0].AssertionAuthorMemberId);
        Assert.Equal(ApproverId, assertion.Current.Approvals[0].ApprovedByMemberId);
    }

    [Fact]
    public void ShouldRejectMissingControlOrCriteriaBindingsGivenAssertionContent()
    {
        // Arrange
        var assertion = new ManagementAssertionAggregate(TenantId, ProgramId, AssertionId);

        // Act
        var failure = assertion.Create(Content() with
        {
            SupportingInputs = [Reference("known_exception")],
        }, AuthorId, "CEO", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, assertion.Revision);
    }

    [Fact]
    public void ShouldReplayExactAssertionVersionAndIndependentApprovalGivenEvents()
    {
        // Arrange
        var assertion = NewAssertion(Content());
        Assert.Null(assertion.Approve(assertion.Revision, 1, "approve",
            "Reviewed the exact assertion", ApproverId, "CISO", "Approver", Now.AddDays(1)));
        var events = new AggregateScenario<ManagementAssertionAggregate>(assertion).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ManagementAssertionAggregate>(
            new ManagementAssertionAggregate(TenantId, ProgramId, AssertionId)).Given(events).Aggregate;

        // Assert
        Assert.Equal("approved", replay.Current!.Status);
        Assert.Equal(assertion.Current!.CurrentVersion!.ContentSha256,
            replay.Current.CurrentVersion!.ContentSha256);
        Assert.Equal(ApproverId, Assert.Single(replay.Current.Approvals).ApprovedByMemberId);
    }

    [Fact]
    public void ShouldTreatRepeatedSameSignerDecisionAsIdempotentGivenLaterRetryTime()
    {
        // Arrange
        var assertion = NewAssertion(Content());
        Assert.Null(assertion.Approve(assertion.Revision, 1, "approve",
            "Reviewed the exact assertion", ApproverId, "CISO", "Approver", Now.AddDays(1)));
        var revision = assertion.Revision;

        // Act
        var retry = assertion.Approve(assertion.Revision, 1, "approve",
            "Reviewed the exact assertion", ApproverId, "ciso", "Approver", Now.AddDays(2));

        // Assert
        Assert.Null(retry);
        Assert.Equal(revision, assertion.Revision);
        Assert.Single(assertion.Current!.Approvals);
        Assert.Equal(Now.AddDays(1), assertion.Current.Approvals[0].ApprovedAt);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(assertion.Approve(assertion.Revision, 1, "approve",
                "Changed the decision rationale", ApproverId, "CISO", "Approver",
                Now.AddDays(3))).Code);
        Assert.Equal(revision, assertion.Revision);
    }

    static ManagementAssertionAggregate NewAssertion(ManagementAssertionContent content)
    {
        var assertion = new ManagementAssertionAggregate(TenantId, ProgramId, AssertionId);
        Assert.Null(assertion.Create(content, AuthorId, "CEO", Now));
        return assertion;
    }

    static ManagementAssertionContent Content() => new(ExaminationId, "type_i",
        Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), new string('b', 64),
        Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), new string('c', 64), "auditor",
        Uuid.CreateVersion4(), "Auditor-approved representation wording",
        [Reference("criteria"), Reference("control"), Reference("known_exception")],
        [new(AuthorId, "CEO"), new(ApproverId, "CISO")]);

    static ExaminationContentReference Reference(string sourceType) =>
        new(sourceType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('d', 64));
}
