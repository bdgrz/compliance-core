using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentVersionProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid DraftId = CommitmentDraft.IdFor(TenantId, ProgramId,
        "user_entity_responsibility", "CUEC-01");

    [Fact]
    public void ShouldProjectImmutableVersionAndDecisionsGivenReviewHistory()
    {
        // Arrange
        // Legacy one-step reviews retain their original immutable business-content interpretation.
        // Fresh native writes require the separate approval exercised by the next acceptance case.
        var rules = new CommitmentRules(TenantId, DraftId);
        var changesRequested = Reviewed(1, "request_changes", null, null);
        var accepted = Reviewed(2, "accept", 1, new DateOnly(2027, 1, 1));
        rules.Apply(new CommitmentDraftCreatedContent(TenantId, ProgramId, DraftId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "user_entity_responsibility", "CUEC-01", "First", "Context", "MSA 4.1",
            AuthorId, "Author", Now));
        rules.Apply(changesRequested);
        rules.Apply(new CommitmentDraftRevisedContent(TenantId, ProgramId, DraftId, 2, "Second", "Context", "MSA 4.2",
            AuthorId, "Author", Now.AddMinutes(1)));

        // Act
        rules.Apply(accepted);
        var version = rules.FindVersion(1);
        var decisions = rules.ReadDecisions();
        var draft = rules.DraftView();

        // Assert
        Assert.NotNull(version);
        Assert.Equal("Second", version.Statement);
        Assert.Equal(2, version.Revision);
        Assert.Equal("user_entity", version.PerformedBy);
        Assert.False(version.InternallyPerformed);
        Assert.Equal(accepted.DecisionId, version.Decision.DecisionId);
        Assert.Single(rules.ReadVersions());
        Assert.Equal(2, decisions.Count);
        Assert.Equal("effective", draft?.Status);
        Assert.Equal("verified", draft?.OwnerResolution);
        Assert.Equal("applicable", draft?.ApplicabilityResolution);
    }

    [Fact]
    public async Task ShouldProjectApprovedVersionWithVerifiedSourceGivenSeparateApproval()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);
        var rules = new CommitmentRules(TenantId, DraftId);
        Assert.True(rules.Create(ProgramId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "user_entity_responsibility",
            "CUEC-01", "First", "Context", "MSA 4.1", AuthorId, "Author", Now).IsSuccess);
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(rules.Review(ProgramId, 1, reviewId, "accept", "Customer IT", "applicable", "supported", null,
            "Verified", ReviewerId, "Reviewer", Now.AddMinutes(2), sourceVerifiedReference: "MSA 4.1", sourceEvidence: "Signed MSA v3"));
        var beforeApproval = await native.PublishAsync(rules);
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, TenantId, beforeApproval);
        var pending = await directory.GetAsync(TenantId, DraftId);
        var approvedId = Uuid.CreateVersion4();
        Assert.Null(rules.Approve(ProgramId, 1, approvedId, reviewId, new DateOnly(2027, 1, 1), "Approved", "digest",
            Uuid.CreateVersion4(), "Approver", Now.AddMinutes(3)));
        var approved = await native.PublishAsync(rules);
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, TenantId, approved);

        // Act
        var version = await directory.GetVersionAsync(TenantId, DraftId, 1);
        var decisions = await directory.ListDecisionsAsync(TenantId, DraftId, 50, null);
        var draft = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal("reviewed", pending?.Status);
        Assert.Equal("verified", pending?.SourceResolution);
        Assert.NotNull(version);
        Assert.Equal("verified", version.SourceResolution);
        Assert.Equal("Signed MSA v3", version.SourceEvidence);
        Assert.Equal(reviewId, version.Decision.DecisionId);
        Assert.Equal(approvedId, version.Approval?.DecisionId);
        Assert.Equal("approval", version.Approval?.Stage);
        Assert.Equal(2, decisions.Items.Count);
        Assert.Equal("effective", draft?.Status);
    }

    static CommitmentReviewedContent Reviewed(long revision, string outcome, long? version,
        DateOnly? effectiveFrom) =>
        new(TenantId, ProgramId, DraftId, revision, Uuid.CreateVersion4(), outcome,
            version is null ? null : "Customer IT", version is null ? null : "applicable",
            version is null ? null : "supported", null, "Rationale", version, effectiveFrom,
            version is null ? null : "digest", ReviewerId, "Reviewer", Now.AddMinutes(revision * 2));

}
