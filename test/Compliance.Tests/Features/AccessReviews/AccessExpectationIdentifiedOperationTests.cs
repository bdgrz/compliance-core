using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>
///     Expectations are owned per expectation line and missing-population exceptions per exception id. An exact retry
///     returns the original frozen result even after the owner changed, the same identity with a changed intent
///     conflicts, unrelated lines and exceptions never contend, and successors serialize on their predecessor's line.
/// </summary>
public sealed class AccessExpectationIdentifiedOperationTests
{
    static readonly AccessExpectationParameters NoTokens = new(PrincipalKind: "access_token");
    static readonly AccessExpectationParameters NoAdmin = new(EntitlementKind: "admin_privilege");

    static string Wire<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);

    [Fact]
    public async Task ShouldReturnOriginalViewGivenExactProposalRetryAfterLaterLineChange()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var propose = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens);
        var first = await fixture.SendAsync(fixture.ManagerUserId, propose);
        await fixture.SendAsync(fixture.ApproverUserId, fixture.Approve(first.ExpectationId, first.Revision));

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, propose);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal("proposed", retried.Status);
        Assert.Equal(1, retried.Revision);
        var current = Assert.Single(listed.Expectations);
        Assert.Equal("approved", current.Status);
        Assert.Equal(2, current.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameProposalIdentityWithChangedIntentOrOriginalRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var propose = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens);
        await fixture.SendAsync(fixture.ManagerUserId, propose);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, propose with { Rationale = "A different rationale." },
            RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, propose with { ExpectedRevision = 1 }, RequestErrorKind.Conflict);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        var only = Assert.Single(listed.Expectations);
        Assert.Equal(1, only.Revision);
        Assert.Equal("Initial expectation.", only.Rationale);
    }

    [Fact]
    public async Task ShouldReturnOriginalViewGivenExactApprovalRetryAfterLaterException()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var proposed = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens));
        var approve = fixture.Approve(proposed.ExpectationId, proposed.Revision);
        var first = await fixture.SendAsync(fixture.ApproverUserId, approve);
        await fixture.SendAsync(fixture.ApproverUserId, new ExemptAccessExpectation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.InstanceId, proposed.ExpectationId, first.Revision, "ada", "Migration.",
            DateTimeOffset.UtcNow.AddDays(10)));

        // Act
        var retried = await fixture.SendAsync(fixture.ApproverUserId, approve);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(2, retried.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameApprovalIdentityWithChangedOriginalRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var proposed = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens));
        var approve = fixture.Approve(proposed.ExpectationId, proposed.Revision);
        await fixture.SendAsync(fixture.ApproverUserId, approve);

        // Act
        await fixture.FailAsync(fixture.ApproverUserId, approve with { ExpectedRevision = 5 }, RequestErrorKind.Conflict);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Equal(2, Assert.Single(listed.Expectations).Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalExceptionGivenExactRetryAndConflictGivenChangedIntent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var approved = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens);
        var exempt = new ExemptAccessExpectation(Uuid.CreateVersion4(), fixture.TenantId, fixture.InstanceId,
            approved.ExpectationId, approved.Revision, "ada", "Migration.", DateTimeOffset.UtcNow.AddDays(10));
        var first = await fixture.SendAsync(fixture.ApproverUserId, exempt);

        // Act
        var retried = await fixture.SendAsync(fixture.ApproverUserId, exempt);
        await fixture.FailAsync(fixture.ApproverUserId, exempt with { Rationale = "Changed." }, RequestErrorKind.Conflict);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(exempt.CommandId, first.ExceptionId);
        Assert.Single(listed.Exceptions);
    }

    [Fact]
    public async Task ShouldKeepUnrelatedExpectationLinesIndependentGivenSeparateRoots()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();

        // Act
        var first = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens));
        var second = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.PrivilegedEntitlement, NoAdmin));
        var secondApproved = await fixture.SendAsync(fixture.ApproverUserId,
            fixture.Approve(second.ExpectationId, second.Revision));
        var firstApproved = await fixture.SendAsync(fixture.ApproverUserId,
            fixture.Approve(first.ExpectationId, first.Revision));

        // Assert
        Assert.Equal(1, first.Revision);
        Assert.Equal(1, second.Revision);
        Assert.Equal(2, secondApproved.Revision);
        Assert.Equal(2, firstApproved.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenTwoSuccessorsRacingOneAcceptedPredecessor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var predecessor = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens);
        var firstSuccessor = await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.PrivilegedEntitlement, NoAdmin, revision: predecessor.Revision,
            supersedes: predecessor.ExpectationId, effectiveFrom: AccessReviewFixture.Observed));

        // Act
        var loser = await fixture.FailAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.PrivilegedEntitlement, new AccessExpectationParameters(EntitlementKind: "permission"),
            revision: predecessor.Revision, supersedes: predecessor.ExpectationId,
            effectiveFrom: AccessReviewFixture.Observed), RequestErrorKind.Conflict);
        var approvedSuccessor = await fixture.SendAsync(fixture.ApproverUserId,
            fixture.Approve(firstSuccessor.ExpectationId, firstSuccessor.Revision));
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Contains("revision", loser.Message, StringComparison.Ordinal);
        Assert.Equal(predecessor.ExpectationId, firstSuccessor.SupersedesExpectationId);
        Assert.Equal(4, approvedSuccessor.Revision);
        Assert.Equal(2, listed.Expectations.Count);
        Assert.All(listed.Expectations, expectation => Assert.Equal(4, expectation.Revision));
    }

    [Fact]
    public async Task ShouldRejectSuccessorGivenUnapprovedOrUnknownPredecessor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var pending = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens));

        // Act
        var unapproved = await fixture.FailAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.PrivilegedEntitlement, NoAdmin, revision: pending.Revision,
            supersedes: pending.ExpectationId), RequestErrorKind.NotFound);
        var unknown = await fixture.FailAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.PrivilegedEntitlement, NoAdmin, supersedes: Uuid.CreateVersion4()),
            RequestErrorKind.NotFound);

        // Assert
        Assert.NotNull(unapproved);
        Assert.NotNull(unknown);
    }

    [Fact]
    public async Task ShouldRejectExceptionGivenUnapprovedSuccessorWrongInstanceOrPastExpiry()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var predecessor = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens);
        var successor = await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.PrivilegedEntitlement, NoAdmin, revision: predecessor.Revision,
            supersedes: predecessor.ExpectationId, effectiveFrom: AccessReviewFixture.Observed));

        // Act
        var onSuccessor = await fixture.FailAsync(fixture.ApproverUserId, new ExemptAccessExpectation(
            Uuid.CreateVersion4(), fixture.TenantId, fixture.InstanceId, successor.ExpectationId, successor.Revision,
            "ada", "Migration.", DateTimeOffset.UtcNow.AddDays(10)), RequestErrorKind.NotFound);
        var wrongInstance = await fixture.FailAsync(fixture.ApproverUserId, new ExemptAccessExpectation(
            Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), predecessor.ExpectationId,
            predecessor.Revision, "ada", "Migration.", DateTimeOffset.UtcNow.AddDays(10)), RequestErrorKind.NotFound);
        var expired = await fixture.FailAsync(fixture.ApproverUserId, new ExemptAccessExpectation(
            Uuid.CreateVersion4(), fixture.TenantId, fixture.InstanceId, predecessor.ExpectationId,
            successor.Revision, "ada", "Migration.", DateTimeOffset.UtcNow.AddDays(-1)), RequestErrorKind.Validation);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.NotNull(onSuccessor);
        Assert.NotNull(wrongInstance);
        Assert.Contains("future", expired.Message, StringComparison.Ordinal);
        Assert.Empty(listed.Exceptions);
    }

    [Fact]
    public async Task ShouldRejectApprovalGivenProposerWithUnknownWaiverOrWrongInstance()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var proposed = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, NoTokens));

        // Act
        var self = await fixture.FailAsync(fixture.ManagerUserId, fixture.Approve(proposed.ExpectationId,
            proposed.Revision), RequestErrorKind.Forbidden);
        var waived = await fixture.FailAsync(fixture.ManagerUserId, fixture.Approve(proposed.ExpectationId,
            proposed.Revision, waiverId: Uuid.CreateVersion4()), RequestErrorKind.Forbidden);
        var wrongInstance = await fixture.FailAsync(fixture.ApproverUserId, new ApproveAccessExpectation(
            Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), proposed.ExpectationId,
            proposed.Revision), RequestErrorKind.NotFound);
        var stale = await fixture.FailAsync(fixture.ApproverUserId, fixture.Approve(proposed.ExpectationId, 4),
            RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("proposed", self.Message, StringComparison.Ordinal);
        Assert.Contains("waiver", waived.Message, StringComparison.Ordinal);
        Assert.NotNull(wrongInstance);
        Assert.Contains("revision", stale.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnOriginalExceptionGivenExactPopulationExceptionRetryAfterOthers()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var exempt = new ExemptMissingAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ApplicationId,
            fixture.InstanceId, 0, "Export is unavailable.", DateTimeOffset.UtcNow.AddDays(10));
        var first = await fixture.SendAsync(fixture.ManagerUserId, exempt);
        var other = await fixture.SendAsync(fixture.ManagerUserId, exempt with
        {
            ExceptionId = Uuid.CreateVersion4(),
            Reason = "A second independent exception.",
        });

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, exempt);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(exempt.ExceptionId, first.ExceptionId);
        Assert.NotEqual(first.ExceptionId, other.ExceptionId);
        Assert.Equal(2, listed.PopulationExceptions.Count);
    }

    [Fact]
    public async Task ShouldConflictGivenSamePopulationExceptionIdentityWithChangedIntentOrNonzeroRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var exempt = new ExemptMissingAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ApplicationId,
            fixture.InstanceId, 0, "Export is unavailable.", DateTimeOffset.UtcNow.AddDays(10));
        await fixture.SendAsync(fixture.ManagerUserId, exempt);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, exempt with { Reason = "Changed." }, RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, exempt with { ExpectedRevision = 1 }, RequestErrorKind.Conflict);
        var nonzero = await fixture.FailAsync(fixture.ManagerUserId, exempt with
        {
            ExceptionId = Uuid.CreateVersion4(),
            ExpectedRevision = 3,
        }, RequestErrorKind.Conflict);
        var expired = await fixture.FailAsync(fixture.ManagerUserId, exempt with
        {
            ExceptionId = Uuid.CreateVersion4(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
        }, RequestErrorKind.Validation);
        var listed = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Contains("expects revision 0", nonzero.Message, StringComparison.Ordinal);
        Assert.Contains("future", expired.Message, StringComparison.Ordinal);
        Assert.Equal("Export is unavailable.", Assert.Single(listed.PopulationExceptions).Reason);
    }
}
