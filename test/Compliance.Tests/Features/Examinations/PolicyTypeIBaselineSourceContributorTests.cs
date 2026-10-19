using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class PolicyTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateOnly VersionStart = new(2027, 1, 1);
    static readonly DateTimeOffset ApprovalAt = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureEffectiveApprovedVersionGivenSuccessorApprovedAfterTypeIDate()
    {
        // Arrange
        var policy = ApprovedPolicy("POL-AC", VersionStart, ApprovalAt);
        var contributor = Contributor(policy);
        var beforeSuccessor = await contributor.ReadAsync(Engagement(TypeIDate),
            CancellationToken.None);
        var afterTypeIDate = new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Access Control Policy v2"),
            Actor(AuthorId), AuthorId, afterTypeIDate));
        ApproveDraft(policy, new DateOnly(2027, 5, 1), afterTypeIDate.AddMinutes(1),
            "impact-digest");

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(beforeSuccessor.IsSuccess, beforeSuccessor.Error?.Message);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("policies", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("policy_version", source.Kind);
        Assert.Equal(policy.Id, source.RecordId);
        Assert.Equal(Uuid.CreateVersion5(policy.Id, "policy_version_v1:1"), source.VersionId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Assert.Single(beforeSuccessor.Value.Sources).ContentSha256,
            source.ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldExcludeApprovalMadeAfterTypeIDateGivenBackdatedEffectiveDate()
    {
        // Arrange
        var policy = ApprovedPolicy("POL-FUTURE", new DateOnly(2027, 3, 1),
            new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(policy);

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("approved by the Type I date",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldKeepVersionEffectiveGivenRetirementApprovedAfterTypeIDate()
    {
        // Arrange
        var policy = ApprovedPolicy("POL-RETRO", VersionStart, ApprovalAt);
        var afterTypeIDate = new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(policy.ProposeRetirement(ProgramId, 1, new DateOnly(2027, 3, 15),
            "Retired retroactively", Actor(AuthorId), AuthorId, afterTypeIDate));
        var review = Accept(policy, ReviewerId, afterTypeIDate.AddMinutes(1));
        Assert.Null(policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, "Approved after the examination date", Actor(ApproverId), ApproverId,
            afterTypeIDate.AddMinutes(2)));
        var contributor = Contributor(policy);

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("policy_version", source.Kind);
        Assert.Equal(Uuid.CreateVersion5(policy.Id, "policy_version_v1:1"), source.VersionId);
        Assert.DoesNotContain(result.Value.Sources, item => item.Kind == "policy_retirement");
    }

    [Fact]
    public async Task ShouldCaptureScheduledRetirementGivenApprovalKnownOnTypeIDate()
    {
        // Arrange
        var policy = ApprovedPolicy("POL-SCHEDULED", VersionStart, ApprovalAt);
        var approvedAt = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(policy.ProposeRetirement(ProgramId, 1, new DateOnly(2027, 5, 1),
            "Replaced by the handbook", Actor(AuthorId), AuthorId, approvedAt));
        var review = Accept(policy, ReviewerId, approvedAt.AddMinutes(1));
        Assert.Null(policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, "Approved", Actor(ApproverId), ApproverId, approvedAt.AddMinutes(2)));
        var contributor = Contributor(policy);

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["policy_retirement", "policy_version"],
            result.Value.Sources.Select(static source => source.Kind));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("retirement scheduled after the Type I date",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldIgnorePeriodicReviewGivenReviewIsAfterTypeIDate()
    {
        // Arrange
        var policy = ApprovedPolicy("POL-OVERDUE", new DateOnly(2026, 1, 1),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(policy);
        var beforeReview = await contributor.ReadAsync(Engagement(TypeIDate),
            CancellationToken.None);
        Assert.Null(policy.ConfirmPeriodicReview(ProgramId, 1, Uuid.CreateVersion4(),
            "Reviewed after the examination date", Actor(ReviewerId), ReviewerId,
            new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(beforeReview.IsSuccess, beforeReview.Error?.Message);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Assert.Single(beforeReview.Value.Sources).ContentSha256,
            Assert.Single(result.Value.Sources).ContentSha256);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("overdue for periodic review",
                StringComparison.Ordinal));
    }

    static PolicyTypeIBaselineSourceContributor Contributor(Policy policy)
    {
        var directory = new Directory(policy);
        return new PolicyTypeIBaselineSourceContributor(directory, new PolicyReader(policy),
            new PolicyDirectoryReadConsistency(directory, new InMemoryEventStore()));
    }

    static ExaminationEngagementView Engagement(DateOnly periodStart) => new(TenantId,
        ProgramId, Uuid.CreateVersion4(), "type_i", "Audit Firm", periodStart, null,
        ["security"], Uuid.CreateVersion4(), [], "draft", 1, ApprovalAt, AuthorId,
        "Examiner");

    static Policy ApprovedPolicy(string identifier, DateOnly effectiveFrom,
        DateTimeOffset approvedAt)
    {
        var policy = new Policy(TenantId, Policy.IdFor(TenantId, ProgramId, identifier));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            Actor(AuthorId), AuthorId, approvedAt.AddMinutes(-2)).IsSuccess);
        ApproveDraft(policy, effectiveFrom, approvedAt);
        return policy;
    }

    static void ApproveDraft(Policy policy, DateOnly effectiveFrom, DateTimeOffset approvedAt,
        string? impactDigest = null)
    {
        var review = Accept(policy, ReviewerId, approvedAt.AddMinutes(-1));
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), review,
            effectiveFrom, impactDigest is null, "Approved", impactDigest, Actor(ApproverId),
            ApproverId, approvedAt));
    }

    static Uuid Accept(Policy policy, Uuid reviewer, DateTimeOffset decidedAt)
    {
        var decisionId = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, decisionId, "accept", "Reviewed",
            Actor(reviewer), reviewer, decidedAt));
        return decisionId;
    }

    static PolicyContent Content(string title = "Access Control Policy") =>
        new(title, "Govern access", PolicyAudience.CoreSecurity, null, 12,
            "The owner reviews access quarterly.", null, "Security lead", []);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId,
        memberId.ToString());

    sealed class Directory(Policy policy) : IPolicyDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var view = policy.ToView(DateOnly.FromDateTime(ApprovalAt.UtcDateTime));
            var item = new PolicySummaryView(TenantId, ProgramId, policy.Id,
                policy.Identifier!, policy.CurrentVersion?.Content.Title ?? policy.DraftContent!.Title,
                policy.Status, policy.PendingStatus, policy.Revision, policy.CurrentVersion?.Version,
                policy.CurrentVersion?.EffectiveFrom, view.NextReviewDueOn, view.ReviewOverdue,
                view.LastChangedAt);
            return ValueTask.FromResult(new Page<PolicySummaryView>([item], null));
        }
    }

    sealed class PolicyReader(Policy policy) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(policy.Id == aggregate.Id && policy is TAggregate found
                ? found
                : aggregate);
    }
}
