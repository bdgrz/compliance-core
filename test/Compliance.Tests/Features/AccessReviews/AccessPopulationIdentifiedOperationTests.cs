using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>
///     The population and classification commands are identified 200 operations: an exact retry returns the original
///     frozen result even after the owner changed, the same identity with a changed intent conflicts, and
///     classification is a separate owner that never touches the accepted raw population.
/// </summary>
public sealed class AccessPopulationIdentifiedOperationTests
{
    static string Wire<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenExactOpenRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var open = fixture.Open(AccessReviewFixture.Observed, "Export");
        var first = await fixture.SendAsync(fixture.ManagerUserId, open);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(first.PopulationId, 1,
            AccessReviewFixture.StandardFacts()));

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, open);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, first.PopulationId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(1, retried.Revision);
        Assert.Equal(2, view.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameOpenIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var open = fixture.Open(AccessReviewFixture.Observed, "Export");
        var first = await fixture.SendAsync(fixture.ManagerUserId, open);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, open with { Source = "A different export" },
            RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, open with { ObservedAt = AccessReviewFixture.Observed.AddHours(1) },
            RequestErrorKind.Conflict);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, first.PopulationId));

        // Assert
        Assert.Equal(1, view.Revision);
        Assert.Equal("Export", view.Source);
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenExactFactsRetryAfterLaterReplacement()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        var record = fixture.Record(opened.PopulationId, 1, AccessReviewFixture.StandardFacts());
        var first = await fixture.SendAsync(fixture.ManagerUserId, record);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, first.Revision,
            AccessReviewFixture.StandardFacts(raeHasAdmin: false)));

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, record);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(2, retried.Revision);
        Assert.Equal(3, view.Revision);
        Assert.DoesNotContain(view.Facts.Assignments, assignment => assignment.PrincipalProviderSubjectId == "rae");
    }

    [Fact]
    public async Task ShouldConflictGivenSameFactsIdentityWithChangedFactsOrOriginalRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        var record = fixture.Record(opened.PopulationId, 1, AccessReviewFixture.StandardFacts());
        await fixture.SendAsync(fixture.ManagerUserId, record);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, record with { Assignments = [] }, RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, record with { ExpectedRevision = 2 }, RequestErrorKind.Conflict);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.Equal(2, view.Revision);
        Assert.NotEmpty(view.Facts.Assignments);
    }

    [Fact]
    public async Task ShouldRejectFactsGivenStaleRevisionWithoutAppend()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));

        // Act
        var stale = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Record(opened.PopulationId, 7, AccessReviewFixture.StandardFacts()), RequestErrorKind.Conflict);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.Contains("revision", stale.Message, StringComparison.Ordinal);
        Assert.Equal(1, view.Revision);
        Assert.Empty(view.Facts.Principals);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcceptanceGivenExactAcceptRetryAfterLaterClassification()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1,
            AccessReviewFixture.StandardFacts()));
        var accept = new AcceptAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2,
            "Attested.");
        var first = await fixture.SendAsync(fixture.ManagerUserId, accept);
        await fixture.ClassifyAsync(opened.PopulationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, accept);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, opened.PopulationId));
        var snapshot = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, first.SnapshotId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(accept.CommandId, first.SnapshotId);
        Assert.Equal(3, view.Revision);
        Assert.Equal(first.ContentSha256, view.ContentSha256);
        Assert.True(snapshot.HasIntactContent);
    }

    [Fact]
    public async Task ShouldConflictGivenSameAcceptIdentityWithChangedAttestationOrRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1,
            AccessReviewFixture.StandardFacts()));
        var accept = new AcceptAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2,
            "Attested.");
        var first = await fixture.SendAsync(fixture.ManagerUserId, accept);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, accept with { Attestation = "Attested differently." },
            RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, accept with { ExpectedRevision = 1 }, RequestErrorKind.Conflict);
        var view = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.Equal(first.SnapshotId, view.SnapshotId);
        Assert.Equal(3, view.Revision);
    }

    [Fact]
    public async Task ShouldAppendOneAcceptanceGivenTwoIdenticalAcceptsRacing()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1,
            AccessReviewFixture.StandardFacts()));
        var accept = new AcceptAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2,
            "Attested.");

        // Act
        var results = await Task.WhenAll(
            PersonalAccessReviewTransportTests.SendHttpAsync(fixture.Provider, fixture.ManagerUserId, accept),
            PersonalAccessReviewTransportTests.SendHttpAsync(fixture.Provider, fixture.ManagerUserId, accept));
        var population = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(Wire(results[0].Value), Wire(results[1].Value));
        Assert.Equal(3, population.Revision);
        Assert.Equal(3UL, population.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectClassificationGivenPopulationNotAccepted()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1,
            AccessReviewFixture.StandardFacts()));

        // Act
        var rejected = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Classify(opened.PopulationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId),
            RequestErrorKind.Conflict);
        var classification = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPrincipalClassification(fixture.TenantId, opened.PopulationId, "ada"));

        // Assert
        Assert.Contains("accepted", rejected.Message, StringComparison.Ordinal);
        Assert.Equal(0, classification.Revision);
    }

    [Fact]
    public async Task ShouldClassifyIndependentlyGivenTwoSubjectsOfOneAcceptedPopulation()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPopulation(fixture.TenantId, populationId));

        // Act
        var ada = await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        var rae = await fixture.ClassifyAsync(populationId, "rae", AccessReviewVocabulary.Human,
            fixture.ReviewerPersonId);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPopulation(fixture.TenantId, populationId));

        // Assert
        Assert.Equal(1, ada.Sequence);
        Assert.Equal(1, rae.Sequence);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(before.AcceptedFact!.ContentSha256, after.AcceptedFact!.ContentSha256);
    }

    [Fact]
    public async Task ShouldConflictGivenTwoClassificationsRacingOneSubjectRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);

        // Act
        var loser = await fixture.FailAsync(fixture.ManagerUserId, fixture.Classify(populationId, "ada",
            AccessReviewVocabulary.Unclassified), RequestErrorKind.Conflict);
        var next = await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Unclassified, revision: 1);

        // Assert
        Assert.Contains("1 classification", loser.Message, StringComparison.Ordinal);
        Assert.Equal(2, next.Sequence);
    }

    [Fact]
    public async Task ShouldReturnOriginalDecisionGivenExactClassifyRetryAfterLaterDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var classify = fixture.Classify(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        var first = await fixture.SendAsync(fixture.ManagerUserId, classify);
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Unclassified, revision: 1);

        // Act
        var retried = await fixture.SendAsync(fixture.ManagerUserId, classify);
        var principals = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(1, retried.Sequence);
        var ada = Assert.Single(principals.Items, item => item.ProviderSubjectId == "ada");
        Assert.Equal(2, ada.History.Count);
        Assert.Equal("unclassified", ada.Classification);
    }

    [Fact]
    public async Task ShouldConflictGivenSameClassifyIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var classify = fixture.Classify(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        await fixture.SendAsync(fixture.ManagerUserId, classify);

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, classify with { Rationale = "Changed rationale." },
            RequestErrorKind.Conflict);
        await fixture.FailAsync(fixture.ManagerUserId, classify with { ExpectedRevision = 1 }, RequestErrorKind.Conflict);
        var principals = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));

        // Assert
        Assert.Single(Assert.Single(principals.Items, item => item.ProviderSubjectId == "ada").History);
    }

    [Fact]
    public async Task ShouldTreatProviderSubjectIdAsExactOrdinalGivenDifferentCase()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());

        // Act
        var other = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Classify(populationId, "ADA", AccessReviewVocabulary.Human, fixture.AdaPersonId),
            RequestErrorKind.NotFound);
        var ada = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPrincipalClassification(fixture.TenantId, populationId, "ada"));

        // Assert
        Assert.Contains("not found", other.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, ada.Revision);
    }

    [Fact]
    public async Task ShouldAcceptCompleteRawGraphGivenMoreFactsThanTheFormerCap()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        const int count = 700;
        var principals = Enumerable.Range(0, count)
            .Select(index => new AccessPrincipalFact($"user-{index:D4}", "user_account", $"User {index}", "active"))
            .ToArray();
        var entitlements = Enumerable.Range(0, count)
            .Select(index => new AccessEntitlementFact($"permission-{index:D4}", "permission", $"Permission {index}"))
            .ToArray();
        var assignments = Enumerable.Range(0, count)
            .Select(index => new AccessAssignmentFact($"user-{index:D4}", $"permission-{index:D4}"))
            .ToArray();
        var facts = new AccessPopulationFacts(principals, entitlements, [], assignments);

        // Act
        var (populationId, accepted) = await fixture.AcceptAsync(facts);
        var view = await fixture.SendAsync(fixture.ManagerUserId, new GetAccessPopulation(fixture.TenantId, populationId));

        // Assert
        Assert.Equal(count, view.Facts.Principals.Count);
        Assert.Equal(count, view.Facts.Entitlements.Count);
        Assert.Equal(count, view.Facts.Assignments.Count);
        Assert.Equal(count, view.EffectiveAccess.Count);
        Assert.Equal(accepted.ContentSha256, view.ContentSha256);
    }

    [Fact]
    public async Task ShouldRefuseAcceptanceGivenInvalidRawGraphWithoutFreezing()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var opened = await fixture.SendAsync(fixture.ManagerUserId, fixture.Open(AccessReviewFixture.Observed));
        var duplicated = new AccessPopulationFacts(
            [new("ada", "user_account", "Ada", "active"), new("ada", "user_account", "Ada again", "active")],
            [], [], []);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Record(opened.PopulationId, 1, duplicated));
        var accept = new AcceptAccessPopulation(Uuid.CreateVersion4(), fixture.TenantId, opened.PopulationId, 2,
            "Attested.");

        // Act
        await fixture.FailAsync(fixture.ManagerUserId, accept, RequestErrorKind.Validation);
        var snapshot = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, accept.CommandId));
        var population = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPopulation(fixture.TenantId, opened.PopulationId));

        // Assert
        Assert.False(snapshot.IsFrozen);
        Assert.False(population.IsAccepted);
    }
}
