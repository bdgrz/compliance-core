using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
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
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-AC", VersionStart, ApprovalAt);
        var contributor = Contributor(policy, source);
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
        var reference = Assert.Single(result.Value.Sources);
        Assert.Equal("policy_version", reference.Kind);
        Assert.Equal(policy.Id, reference.RecordId);
        Assert.Equal(Uuid.CreateVersion5(policy.Id, "policy_version_v1:1"), reference.VersionId);
        Assert.Equal(1, reference.Revision);
        Assert.Equal(Assert.Single(beforeSuccessor.Value.Sources).ContentSha256,
            reference.ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", reference.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldExcludeApprovalMadeAfterTypeIDateGivenBackdatedEffectiveDate()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-FUTURE", new DateOnly(2027, 3, 1),
            new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(policy, source);

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
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-RETRO", VersionStart, ApprovalAt);
        var afterTypeIDate = new DateTimeOffset(2027, 4, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(policy.ProposeRetirement(ProgramId, 1, new DateOnly(2027, 3, 15),
            "Retired retroactively", Actor(AuthorId), AuthorId, afterTypeIDate));
        var review = Accept(policy, ReviewerId, afterTypeIDate.AddMinutes(1));
        Assert.Null(policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, "Approved after the examination date", Actor(ApproverId), ApproverId,
            afterTypeIDate.AddMinutes(2)));
        var contributor = Contributor(policy, source);

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.Sources);
        Assert.Equal("policy_version", reference.Kind);
        Assert.Equal(Uuid.CreateVersion5(policy.Id, "policy_version_v1:1"), reference.VersionId);
        Assert.DoesNotContain(result.Value.Sources, item => item.Kind == "policy_retirement");
    }

    [Fact]
    public async Task ShouldCaptureScheduledRetirementGivenApprovalKnownOnTypeIDate()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-SCHEDULED", VersionStart, ApprovalAt);
        var approvedAt = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(policy.ProposeRetirement(ProgramId, 1, new DateOnly(2027, 5, 1),
            "Replaced by the handbook", Actor(AuthorId), AuthorId, approvedAt));
        var review = Accept(policy, ReviewerId, approvedAt.AddMinutes(1));
        Assert.Null(policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            review, "Approved", Actor(ApproverId), ApproverId, approvedAt.AddMinutes(2)));
        var contributor = Contributor(policy, source);

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["policy_retirement", "policy_version"],
            result.Value.Sources.Select(static entry => entry.Kind));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("retirement scheduled after the Type I date",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldIgnorePeriodicReviewGivenReviewIsAfterTypeIDate()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-OVERDUE", new DateOnly(2026, 1, 1),
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(policy, source);
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

    [Fact]
    public async Task ShouldExcludeLaterApprovalGivenExactRecordedThroughCutoff()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-RECORDED", VersionStart, ApprovalAt);
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Later approval"), Actor(AuthorId), AuthorId,
            new DateTimeOffset(2027, 3, 15, 12, 0, 0, TimeSpan.Zero)));
        ApproveDraft(policy, new DateOnly(2027, 2, 1), new DateTimeOffset(2027, 3, 15, 12, 2, 0, TimeSpan.Zero),
            "impact-digest");
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [])
        { RecordedThrough = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero) };

        // Act
        var result = await Contributor(policy, source).ReadAsync(Engagement(TypeIDate), context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var captured = Assert.Single(result.Value.Sources);
        Assert.Equal(Uuid.CreateVersion5(policy.Id, "policy_version_v1:1"), captured.VersionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTypedSourceFailureGivenUnreadableNativePolicy(bool unavailable)
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var policy = ApprovedPolicy("POL-FAILURE", VersionStart, ApprovalAt);
        await source.SeedAsync(policy);
        var directory = new Directory(policy, source);
        var store = new UnreadableMutation(source.Content, unavailable);
        var reads = new PolicyReader(source.Reader, source.Events, store);
        var contributor = new PolicyTypeIBaselineSourceContributor(directory, reads,
            new PolicyDirectoryReadConsistency(directory, source.Events));
        var before = await source.Reader.HydrateAsync(new Policy(TenantId, policy.Id));

        // Act
        var result = await contributor.ReadAsync(Engagement(TypeIDate), CancellationToken.None);
        var after = await source.Reader.HydrateAsync(new Policy(TenantId, policy.Id));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(1, store.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    sealed class UnreadableMutation(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != PolicyFactContent.ChangeSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("The native policy mutation is temporarily absent.")
                : new ImmutableContentIntegrityException("The native policy mutation digest differs.");
        }
    }

    static ContributorScenario Contributor(PolicyRules policy, PolicySourceFixture source) => new(policy, source);

    sealed class ContributorScenario(PolicyRules policy, PolicySourceFixture source)
    {
        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement,
            CancellationToken ct) => ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

        public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement,
            TypeIBaselineSourceReadContext context, CancellationToken ct)
        {
            await source.SeedAsync(policy);
            var directory = new Directory(policy, source);
            return await new PolicyTypeIBaselineSourceContributor(directory, source.Policies,
                new PolicyDirectoryReadConsistency(directory, source.Events)).ReadAsync(engagement, context, ct);
        }
    }

    static ExaminationEngagementView Engagement(DateOnly periodStart) => new(TenantId,
        ProgramId, Uuid.CreateVersion4(), "type_i", "Audit Firm", periodStart, null,
        ["security"], Uuid.CreateVersion4(), [], "draft", 1, ApprovalAt, AuthorId,
        "Examiner");

    static PolicyRules ApprovedPolicy(string identifier, DateOnly effectiveFrom,
        DateTimeOffset approvedAt)
    {
        var policy = new PolicyRules(TenantId, Policy.IdFor(TenantId, ProgramId, identifier));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            Actor(AuthorId), AuthorId, approvedAt.AddMinutes(-2)).IsSuccess);
        ApproveDraft(policy, effectiveFrom, approvedAt);
        return policy;
    }

    static void ApproveDraft(PolicyRules policy, DateOnly effectiveFrom, DateTimeOffset approvedAt,
        string? impactDigest = null)
    {
        var review = Accept(policy, ReviewerId, approvedAt.AddMinutes(-1));
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), review,
            effectiveFrom, impactDigest is null, "Approved", impactDigest, Actor(ApproverId),
            ApproverId, approvedAt));
    }

    static Uuid Accept(PolicyRules policy, Uuid reviewer, DateTimeOffset decidedAt)
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

    sealed class Directory(PolicyRules policy, PolicySourceFixture source) : IPolicyDirectoryReader
    {
        public async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default)
        {
            var cursor = EventCursor.Start;
            await foreach (var record in source.Events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(), "policies"),
                EventCursor.Start, ct))
                cursor = record.NextCursor;
            return new ProjectionCheckpoint(cursor);
        }

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

}
