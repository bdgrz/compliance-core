using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntryEditionLifecycleTests
{
    [Fact]
    public void ShouldKeepDraftActiveGivenAnAdmittedRename()
    {
        // Arrange
        var fixture = new Fixture();
        var operation = Uuid.CreateVersion4();
        var request = new BeginCriteriaEditionRename(fixture.Edition, operation, "edition_2");
        var rename = new CriteriaEditionRenameRequested(fixture.Edition, operation, 1, "edition_1", 1,
            request.Label, fixture.Actor, fixture.Admit(operation, 1, request));

        // Act
        var applied = fixture.Lifecycle.Apply(rename);

        // Assert
        Assert.True(applied);
        Assert.True(fixture.Lifecycle.Active);
    }

    [Fact]
    public void ShouldRejectForgedRenameAdmissionGivenChangedTargetOrIntent()
    {
        // Arrange
        var fixture = new Fixture();
        var operation = Uuid.CreateVersion4();
        var request = new BeginCriteriaEditionRename(fixture.Edition, operation, "edition_2");
        var admission = fixture.Admit(operation, 1, request) with { Target = "stream://bdgrz/other/target" };
        var rename = new CriteriaEditionRenameRequested(fixture.Edition, operation, 1, "edition_1", 1,
            request.Label, fixture.Actor, admission);

        // Act
        var applied = fixture.Lifecycle.Apply(rename);

        // Assert
        Assert.False(applied);
        Assert.True(fixture.Lifecycle.Active);
    }

    [Fact]
    public void ShouldKeepIndependentEntriesActiveGivenAnAdmittedPublicationStart()
    {
        // Arrange
        var fixture = new Fixture();
        var attempt = Uuid.CreateVersion4();
        var incarnation = Uuid.CreateVersion4();
        var request = new BeginCriteriaPublication(fixture.Edition, attempt, "CC6.1", incarnation);
        var started = new CriteriaPublicationStarted(fixture.Edition, fixture.Draft, attempt, 1, 1, "CC6.1", incarnation,
            "edition_1", 1, fixture.Confirmation, fixture.Actor, fixture.Admit(attempt, 1, request));

        // Act
        var applied = fixture.Lifecycle.Apply(started);

        // Assert
        Assert.True(applied);
        Assert.True(fixture.Lifecycle.Active);
    }

    [Fact]
    public void ShouldRejectPublicationStartGivenEvidenceForDifferentWitness()
    {
        // Arrange
        var fixture = new Fixture();
        var attempt = Uuid.CreateVersion4();
        var incarnation = Uuid.CreateVersion4();
        var authorized = new BeginCriteriaPublication(fixture.Edition, attempt, "CC6.1", incarnation);
        var started = new CriteriaPublicationStarted(fixture.Edition, fixture.Draft, attempt, 1, 1, "CC6.2", incarnation,
            "edition_1", 1, fixture.Confirmation, fixture.Actor, fixture.Admit(attempt, 1, authorized));

        // Act
        var applied = fixture.Lifecycle.Apply(started);

        // Assert
        Assert.False(applied);
        Assert.True(fixture.Lifecycle.Active);
    }

    [Fact]
    public void ShouldRejectOlderAdmissionGivenACommittedExecutionFence()
    {
        // Arrange
        var fixture = new Fixture();
        var fence = new CriteriaEditionOperationAttemptFenced(fixture.Edition, Uuid.CreateVersion4(), 1, 3,
            new string('a', 64));
        fence.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), new CriteriaEdition(fixture.Edition).Id,
            4, DateTimeOffset.UtcNow));
        Assert.True(fixture.Lifecycle.Apply(fence));
        var operation = Uuid.CreateVersion4();
        var request = new BeginCriteriaEditionRename(fixture.Edition, operation, "edition_2");
        var stale = new CriteriaEditionRenameRequested(fixture.Edition, operation, 1, "edition_1", 1,
            request.Label, fixture.Actor, fixture.Admit(operation, 1, request));
        var current = stale with { ExecutionGeneration = 2, Admission = fixture.Admit(operation, 2, request) };

        // Act
        var rejected = fixture.Lifecycle.Apply(stale);
        var accepted = fixture.Lifecycle.Apply(current);

        // Assert
        Assert.False(rejected);
        Assert.True(accepted);
        Assert.True(fixture.Lifecycle.Active);
    }

    [Fact]
    public void ShouldRejectUnsignedPublicationGivenHumanDraft()
    {
        // Arrange
        var fixture = new Fixture();
        var started = new CriteriaPublicationStarted(fixture.Edition, fixture.Draft, Uuid.CreateVersion4(), 1, 1,
            "CC6.1", Uuid.CreateVersion4(), "edition_1", 1, fixture.Confirmation,
            new ActorAttribution("criteria-seed-bootstrap", "system"), null);

        // Act
        var applied = fixture.Lifecycle.Apply(started);

        // Assert
        Assert.False(applied);
    }

    [Fact]
    public void ShouldPermitTrustedSeedStartGivenExactConfiguredSeedBinding()
    {
        // Arrange
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition");
        var fingerprint = CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var actor = new ActorAttribution("criteria-seed-bootstrap", "system");
        var lifecycle = new CriteriaEntryEditionLifecycle(seed.EditionId, operation);
        var requested = new CriteriaEditionDraftRequested(seed.EditionId, operation, operation, 1, seed.Framework,
            seed.EditionLabel, actor, null, CriteriaSeedFingerprint.Version, fingerprint);
        Assert.True(lifecycle.Apply(requested));
        Assert.True(lifecycle.Apply(new CriteriaEditionDraftActivated(seed.EditionId, operation, operation, seed.Framework,
            seed.EditionLabel, 1, Uuid.CreateVersion4())));
        var confirmation = Uuid.CreateVersion4();
        Assert.True(lifecycle.Apply(new CriteriaEditionDrafted(seed.EditionId, operation, operation, seed.Framework,
            seed.EditionLabel, 1, confirmation)));
        var started = new CriteriaPublicationStarted(seed.EditionId, operation, Uuid.CreateVersion4(), 1, 1,
            "CC6.1", Uuid.CreateVersion4(), seed.EditionLabel, 1, confirmation, actor, null);

        // Act
        var applied = lifecycle.Apply(started);

        // Assert
        Assert.True(applied);
        Assert.True(lifecycle.Active);
        Assert.False(lifecycle.ValueActive);
    }

    [Fact]
    public void ShouldRejectSeedRequestGivenDifferentFingerprint()
    {
        // Arrange
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition");
        var lifecycle = new CriteriaEntryEditionLifecycle(seed.EditionId, operation);
        var requested = new CriteriaEditionDraftRequested(seed.EditionId, operation, operation, 1, seed.Framework,
            seed.EditionLabel, new ActorAttribution("criteria-seed-bootstrap", "system"), null,
            CriteriaSeedFingerprint.Version, new string('a', 64));

        // Act
        var applied = lifecycle.Apply(requested);

        // Assert
        Assert.False(applied);
        Assert.False(lifecycle.Active);
    }

    [Fact]
    public void ShouldRejectForgedRenameCancellationGivenDifferentAdmittedTarget()
    {
        // Arrange
        var fixture = new Fixture();
        var renameId = Uuid.CreateVersion4();
        var renameRequest = new BeginCriteriaEditionRename(fixture.Edition, renameId, "edition_2");
        Assert.True(fixture.Lifecycle.Apply(new CriteriaEditionRenameRequested(fixture.Edition, renameId, 1,
            "edition_1", 1, "edition_2", fixture.Actor, fixture.Admit(renameId, 1, renameRequest))));
        var cancellationId = Uuid.CreateVersion4();
        var wrong = new CancelCriteriaEditionRename(fixture.Edition, cancellationId, Uuid.CreateVersion4());
        var cancelled = new CriteriaEditionRenameCancelled(fixture.Edition, cancellationId, 1, renameId,
            fixture.Actor, fixture.Admit(cancellationId, 1, wrong));

        // Act
        var applied = fixture.Lifecycle.Apply(cancelled);

        // Assert
        Assert.False(applied);
        Assert.True(fixture.Lifecycle.Active);
    }

    sealed class Fixture
    {
        public Fixture()
        {
            var operation = Draft;
            var request = new DraftCriteriaEdition(Edition, operation, "tsc", "edition_1");
            Assert.True(Lifecycle.Apply(new CriteriaEditionDraftRequested(Edition, Draft, operation, 1, "tsc",
                "edition_1", Actor, Admit(operation, 1, request), null, null)));
            Assert.True(Lifecycle.Apply(new CriteriaEditionDraftActivated(Edition, Draft, operation, "tsc", "edition_1",
                1, Uuid.CreateVersion4())));
            Assert.True(Lifecycle.Apply(new CriteriaEditionDrafted(Edition, Draft, operation, "tsc", "edition_1",
                1, Confirmation)));
        }

        public Uuid Edition { get; } = Uuid.CreateVersion4();
        public Uuid Draft { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Confirmation { get; } = Uuid.CreateVersion4();
        public ActorAttribution Actor => new(User.ToString(), "bdgrz");
        public CriteriaEntryEditionLifecycle Lifecycle => _lifecycle ??= new CriteriaEntryEditionLifecycle(Edition, Draft);
        CriteriaEntryEditionLifecycle? _lifecycle;

        public AccessAdmissionEvidence Admit(Uuid operation, long generation, object request)
        {
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            var digest = request switch
            {
                DraftCriteriaEdition value => AuthoredIntentDigest.Compute(value),
                BeginCriteriaEditionRename value => AuthoredIntentDigest.Compute(value),
                CancelCriteriaEditionRename value => AuthoredIntentDigest.Compute(value),
                BeginCriteriaPublication value => AuthoredIntentDigest.Compute(value),
                _ => throw new InvalidOperationException("Unexpected test request."),
            };
            return new AccessAdmissionEvidence(User, Actor.Subject, Actor.Issuer, operation,
                new CriteriaEdition(Edition).Stream.ToString(), CriteriaEdition.BindAdmissionDigest(digest, generation),
                DateTimeOffset.UtcNow, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
        }
    }
}
