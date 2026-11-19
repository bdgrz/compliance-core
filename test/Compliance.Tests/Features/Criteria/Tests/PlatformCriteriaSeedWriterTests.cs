using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class PlatformCriteriaSeedWriterTests
{
    [Fact]
    public async Task ShouldKeepBindingPendingGivenNoQualifiedEditionHead()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: false);

        // Act
        var result = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal(0, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldBindExactSeedOnceGivenQualifiedHeadAndSystemContext()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);

        // Act
        var first = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var retry = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, first.Status);
        Assert.NotEqual(Uuid.Empty, first.EvidenceId);
        Assert.Equal(first, retry);
        Assert.Equal(1, fixture.Executor.Executions);
        Assert.IsType<CriteriaEditionDraftRequested>(Assert.Single(fixture.Reader.Read(new CriteriaEdition(fixture.Header.EditionId).Stream)));
    }

    [Fact]
    public async Task ShouldNotBindGivenChangedSeedFingerprint()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);

        // Act
        var result = await fixture.Writer.BindEditionAsync(fixture.Header with { SeedFingerprint = new string('a', 64) },
            fixture.Operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Conflict, result.Status);
        Assert.Equal(0, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepBindingPendingGivenUnknownAppendOutcome()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, ambiguous: true);

        // Act
        var result = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
    }

    [Fact]
    public async Task ShouldRejectNonSystemExecutionGivenSystemActorIsRequired()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, system: false);

        // Act
        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None));
        Assert.Equal(0, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepMetadataPendingGivenMetadataAndCoverageAreUnobserved()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var metadataOperation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");

        // Act
        var metadata = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, metadataOperation, CancellationToken.None);
        var coverage = await fixture.Writer.ConfirmCoverageAsync(fixture.Header, binding.EvidenceId, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, metadata.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Pending, coverage.Status);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldKeepMetadataPendingGivenMissingQualifiedHead(bool editionHead, bool factHead)
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var writer = fixture.CreateWriter(editionHead, factHead, qualifyCoverage: true);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");

        // Act
        var result = await writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldNotWriteMetadataGivenUnqualifiedSourceCoverage()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var writer = new PlatformCriteriaSeedWriter(fixture.Context, fixture.Reader, fixture.Executor,
            new MemorySource(fixture.Reader), new Head(), new Coverage(false), seedFactHead: new Head());
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");

        // Act
        var result = await writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldWriteAndConfirmMetadataGivenQualifiedIndependentHeadsAndSourceCut()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);
        var retry = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, result.Status);
        Assert.Equal(result, retry);
        Assert.Equal(2, fixture.Executor.Executions);
        Assert.Single(fixture.Reader.Read(new CriteriaSeedMetadata(fixture.Header.EditionId).Stream));
    }

    [Fact]
    public async Task ShouldNotConfirmMetadataGivenPublicationWinsBeforeAppend()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        fixture.Executor.BeforeAppend = owner =>
        {
            if (owner is not CriteriaSeedMetadata)
                return;
            var edition = new CriteriaEdition(fixture.Header.EditionId);
            var published = DomainEventSeed.Attach(new CriteriaEditionPublished(fixture.Header.EditionId, fixture.Operation,
                Uuid.CreateVersion4(), 1, "CC1.1", Uuid.CreateVersion4(), fixture.Header.EditionLabel, 1,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ActorAttribution("criteria-seed-bootstrap", "system")), edition.Id, 2);
            fixture.Reader.Append(edition.Stream, [published]);
        };

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Single(fixture.Reader.Read(new CriteriaSeedMetadata(fixture.Header.EditionId).Stream));
    }

    [Fact]
    public async Task ShouldConfirmMetadataGivenItCommittedBeforePublication()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        fixture.Executor.AfterAppend = owner =>
        {
            if (owner is not CriteriaSeedMetadata)
                return;
            var edition = new CriteriaEdition(fixture.Header.EditionId);
            var published = DomainEventSeed.Attach(new CriteriaEditionPublished(fixture.Header.EditionId, fixture.Operation,
                Uuid.CreateVersion4(), 1, "CC1.1", Uuid.CreateVersion4(), fixture.Header.EditionLabel, 1,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ActorAttribution("criteria-seed-bootstrap", "system")), edition.Id, 2);
            fixture.Reader.Append(edition.Stream, [published]);
        };

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, result.Status);
        Assert.Equal(2, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldResolveLostMetadataAcknowledgementGivenOnlyTheExactSourceIsObserved()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        fixture.Executor.Ambiguous = true;

        // Act
        var first = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);
        fixture.Executor.Ambiguous = false;
        var retry = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, retry.Status);
        Assert.Equal(2, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldReconcileMetadataWinnerGivenTheConcurrentAppendIsExact()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        fixture.Executor.BeforeAppend = current =>
        {
            if (current is not CriteriaSeedMetadata)
                return;
            var competing = new CriteriaSeedMetadata(fixture.Header.EditionId);
            Assert.True(competing.Record(operation, fixture.Header.SeedVersion, fixture.Header.SeedFingerprint,
                binding.EvidenceId, fixture.Header.Framework, fixture.Header.PublishedAt, fixture.Header.CoverageNote,
                fixture.Header.SourceUrl, fixture.Header.ContentRights, fixture.Header.NumberedCriteriaComplete,
                new ActorAttribution("criteria-seed-bootstrap", "system")).IsSuccess);
            fixture.Reader.Append(competing.Stream, new AggregateScenario<CriteriaSeedMetadata>(competing).PendingEvents);
        };

        // Act
        var first = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);
        fixture.Executor.BeforeAppend = null;
        var retry = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, retry.Status);
        Assert.Equal(2, fixture.Executor.Executions);
        Assert.Single(fixture.Reader.Read(new CriteriaSeedMetadata(fixture.Header.EditionId).Stream));
    }

    [Fact]
    public async Task ShouldWriteOneDisclosureGivenQualifiedIndependentHeadsAndSourceCut()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var gap = PlatformCriteriaCatalog.SeedEdition().SupportGaps[0];
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "disclosure:" + gap.Category + ":" + gap.Code);

        // Act
        var result = await fixture.Writer.EnsureDisclosureAsync(fixture.Header, binding.EvidenceId, operation, gap, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, result.Status);
        Assert.Equal(2, fixture.Executor.Executions);
        Assert.Single(fixture.Reader.Read(new CriteriaSeedDisclosure(fixture.Header.EditionId, gap.Category, gap.Code).Stream));
    }

    [Fact]
    public async Task ShouldNotConfirmDisclosureGivenPublicationWinsBeforeAppend()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var gap = PlatformCriteriaCatalog.SeedEdition().SupportGaps[0];
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "disclosure:" + gap.Category + ":" + gap.Code);
        fixture.Executor.BeforeAppend = owner =>
        {
            if (owner is not CriteriaSeedDisclosure)
                return;
            var edition = new CriteriaEdition(fixture.Header.EditionId);
            var published = DomainEventSeed.Attach(new CriteriaEditionPublished(fixture.Header.EditionId, fixture.Operation,
                Uuid.CreateVersion4(), 1, "CC1.1", Uuid.CreateVersion4(), fixture.Header.EditionLabel, 1,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ActorAttribution("criteria-seed-bootstrap", "system")), edition.Id, 2);
            fixture.Reader.Append(edition.Stream, [published]);
        };

        // Act
        var result = await fixture.Writer.EnsureDisclosureAsync(fixture.Header, binding.EvidenceId, operation, gap, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Single(fixture.Reader.Read(new CriteriaSeedDisclosure(fixture.Header.EditionId, gap.Category, gap.Code).Stream));
    }

    [Fact]
    public async Task ShouldNotConfirmMetadataGivenAbandonmentWinsBeforeAppend()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true, qualifyFactHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        fixture.Executor.BeforeAppend = owner =>
        {
            if (owner is not CriteriaSeedMetadata)
                return;
            var edition = new CriteriaEdition(fixture.Header.EditionId);
            var abandoned = DomainEventSeed.Attach(new CriteriaEditionDraftAbandoned(fixture.Header.EditionId,
                fixture.Operation, Uuid.CreateVersion4(), 1, fixture.Header.EditionLabel, 1,
                new ActorAttribution("operator", "bdgrz"), null!), edition.Id, 2);
            fixture.Reader.Append(edition.Stream, [abandoned]);
        };

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
    }

    [Fact]
    public async Task ShouldConfirmMetadataGivenSourceCoverageIsQualified()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        var owner = new CriteriaSeedMetadata(fixture.Header.EditionId);
        Assert.True(owner.Record(operation, fixture.Header.SeedVersion, fixture.Header.SeedFingerprint, binding.EvidenceId,
            fixture.Header.Framework, fixture.Header.PublishedAt, fixture.Header.CoverageNote, fixture.Header.SourceUrl,
            fixture.Header.ContentRights, fixture.Header.NumberedCriteriaComplete,
            new ActorAttribution("criteria-seed-bootstrap", "system")).IsSuccess);
        fixture.Reader.Append(owner.Stream, new AggregateScenario<CriteriaSeedMetadata>(owner).PendingEvents);

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, result.Status);
        Assert.NotEqual(Uuid.Empty, result.EvidenceId);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepObservedMetadataPendingGivenUnqualifiedCoverage()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "metadata");
        var owner = new CriteriaSeedMetadata(fixture.Header.EditionId);
        Assert.True(owner.Record(operation, fixture.Header.SeedVersion, fixture.Header.SeedFingerprint, binding.EvidenceId,
            fixture.Header.Framework, fixture.Header.PublishedAt, fixture.Header.CoverageNote, fixture.Header.SourceUrl,
            fixture.Header.ContentRights, fixture.Header.NumberedCriteriaComplete,
            new ActorAttribution("criteria-seed-bootstrap", "system")).IsSuccess);
        fixture.Reader.Append(owner.Stream, new AggregateScenario<CriteriaSeedMetadata>(owner).PendingEvents);

        // Act
        var result = await fixture.Writer.EnsureMetadataAsync(fixture.Header, binding.EvidenceId, operation, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
    }

    [Fact]
    public async Task ShouldConfirmOneExactDisclosureGivenQualifiedSource()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var gap = PlatformCriteriaCatalog.SeedEdition().SupportGaps[0];
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId,
            "disclosure:" + gap.Category + ":" + gap.Code);
        var owner = new CriteriaSeedDisclosure(fixture.Header.EditionId, gap.Category, gap.Code);
        Assert.True(owner.Record(operation, fixture.Header.SeedVersion, fixture.Header.SeedFingerprint,
            binding.EvidenceId, gap.Note, new ActorAttribution("criteria-seed-bootstrap", "system")).IsSuccess);
        fixture.Reader.Append(owner.Stream, new AggregateScenario<CriteriaSeedDisclosure>(owner).PendingEvents);

        // Act
        var result = await fixture.Writer.EnsureDisclosureAsync(fixture.Header, binding.EvidenceId, operation, gap, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepEntryPendingGivenTheEntryIsUnobserved()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true, qualifyCoverage: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);

        // Act
        var result = await fixture.Writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
        Assert.Empty(fixture.Reader.Read(new CriteriaEntry(entry.EditionId, entry.Identifier).Stream));
    }

    [Fact]
    public async Task ShouldRejectSeedEntryGivenItsContentChanged()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);

        // Act
        var result = await fixture.Writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation,
            entry with { Summary = "Different." }, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Conflict, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepSeedOutcomePendingGivenOneEntryIsClaimedAndProposed()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);

        // Act
        var first = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);
        var retry = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Pending, retry.Status);
        var claimOwner = new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation);
        var claim = Assert.IsType<CriterionEntryOperationSeedClaimed>(Assert.Single(fixture.Reader.Read(claimOwner.Stream)));
        var target = new CriteriaEntry(entry.EditionId, entry.Identifier, operation);
        var proposal = Assert.IsType<CriterionSeedProposed>(Assert.Single(fixture.Reader.Read(target.Stream)));
        Assert.Equal(claim.Metadata.EventId, proposal.OperationClaimEventId);
        Assert.Equal(binding.EvidenceId, proposal.EditionRequestEventId);
        Assert.Equal(3, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldLeaveEntryPendingGivenClaimAppendAcknowledgementIsLost()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);
        fixture.Executor.Ambiguous = true;

        // Act
        var first = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);
        fixture.Executor.Ambiguous = false;
        var retry = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Pending, retry.Status);
        Assert.Single(fixture.Reader.Read(new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Single(fixture.Reader.Read(new CriteriaEntry(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Equal(3, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldNotClaimPointOfFocusGivenUnobservedParentAcceptance()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "point_of_focus");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);

        // Act
        var result = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal(1, fixture.Executor.Executions);
        Assert.Empty(fixture.Reader.Read(new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation).Stream));
    }

    [Fact]
    public async Task ShouldLeaveEntryPendingGivenUnqualifiedClaimHead()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter(qualifyFactHead: false);
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);

        // Act
        var result = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Empty(fixture.Reader.Read(new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Equal(1, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldReconcileProposalGivenLostTargetAppendAcknowledgement()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);
        fixture.Executor.BeforeAppend = owner =>
        {
            if (owner is CriteriaEntry)
                fixture.Executor.Ambiguous = true;
        };

        // Act
        var first = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);
        fixture.Executor.BeforeAppend = null;
        fixture.Executor.Ambiguous = false;
        var retry = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Pending, retry.Status);
        Assert.Single(fixture.Reader.Read(new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Single(fixture.Reader.Read(new CriteriaEntry(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Equal(3, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldReconcileClaimGivenCompetingExactAppend()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "entry:" + entry.Identifier);
        fixture.Executor.BeforeAppend = owner =>
        {
            if (owner is not CriteriaEntryOperation claim)
                return;
            fixture.Executor.BeforeAppend = null;
            fixture.Reader.Append(claim.Stream, new AggregateScenario<CriteriaEntryOperation>(claim).PendingEvents);
        };

        // Act
        var first = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);
        var retry = await writer.EnsureEntryAsync(fixture.Header, binding.EvidenceId, operation, entry, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, first.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Pending, retry.Status);
        Assert.Single(fixture.Reader.Read(new CriteriaEntryOperation(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Single(fixture.Reader.Read(new CriteriaEntry(entry.EditionId, entry.Identifier, operation).Stream));
        Assert.Equal(3, fixture.Executor.Executions);
    }

    [Fact]
    public async Task ShouldKeepPublicationPendingGivenUnqualifiedLabelHead()
    {
        // Arrange
        var fixture = new Fixture(qualifyHead: true);
        var binding = await fixture.Writer.BindEditionAsync(fixture.Header, fixture.Operation, CancellationToken.None);
        fixture.SetQualifiedDraft();
        var writer = fixture.CreateEntryWriter();
        var witness = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(fixture.Header.EditionId, "publication");

        // Act
        var result = await writer.PublishAsync(fixture.Header, binding.EvidenceId, operation, witness.Identifier, CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.DoesNotContain(fixture.Reader.Read(new CriteriaEdition(fixture.Header.EditionId).Stream),
            fact => fact is CriteriaPublicationStarted);
        Assert.Equal(1, fixture.Executor.Executions);
    }

    sealed class Fixture
    {
        public Fixture(bool qualifyHead, bool ambiguous = false, bool system = true, bool qualifyCoverage = false,
            bool qualifyFactHead = false)
        {
            var seed = PlatformCriteriaCatalog.SeedEdition();
            Header = new CriteriaSeedHeader(seed.EditionId, seed.Framework, seed.EditionLabel, seed.PublishedAt,
                seed.IsComplete, seed.CoverageNote, seed.SourceUrl, seed.ContentRights, CriteriaSeedFingerprint.Version,
                CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()));
            Operation = PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition");
            Reader = new MemoryReader();
            Executor = new MemoryExecutor(Reader) { Ambiguous = ambiguous };
            var request = new SetCriteriaEditionField(seed.EditionId, Uuid.CreateVersion4(), "coverage_note",
                Uuid.CreateVersion4(), 0, "unused", null);
            Context = new RequestContext<SetCriteriaEditionField>(request, system ? RequestActor.System : RequestActor.Anonymous);
            Writer = CreateWriter(qualifyHead, qualifyFactHead, qualifyCoverage);
        }

        public CriteriaSeedHeader Header { get; }
        public Uuid Operation { get; }
        public MemoryReader Reader { get; }
        public MemoryExecutor Executor { get; }
        public PlatformCriteriaSeedWriter Writer { get; }
        public IExecutionContext Context { get; }
        public PlatformCriteriaSeedWriter CreateWriter(bool qualifyEditionHead, bool qualifyFactHead, bool qualifyCoverage) =>
            new(Context, Reader, Executor, new MemorySource(Reader), qualifyEditionHead ? new Head() : null,
                qualifyCoverage ? new Coverage() : null, seedFactHead: qualifyFactHead ? new Head() : null);

        public PlatformCriteriaSeedWriter CreateEntryWriter(bool qualifyFactHead = true) =>
            new(Context, Reader, Executor, new MemorySource(Reader), new Head(), new Coverage(), new Publications(),
                new Causality(), qualifyFactHead ? new Head() : null);

        public void SetQualifiedDraft()
        {
            var edition = new CriteriaEdition(Header.EditionId);
            var request = Assert.IsType<CriteriaEditionDraftRequested>(Assert.Single(Reader.Read(edition.Stream)));
            var label = new EditionLabelClaim(Header.EditionLabel);
            var reserved = DomainEventSeed.Attach(new EditionLabelReserved(Header.EditionLabel, Header.EditionId, Operation,
                1, request.Metadata.EventId, new ActorAttribution("criteria-seed-bootstrap", "system")), label.Id, 1);
            Reader.Append(label.Stream, [reserved]);
            var activated = DomainEventSeed.Attach(new CriteriaEditionDraftActivated(Header.EditionId, Operation, Operation,
                "tsc", Header.EditionLabel, 1, reserved.Metadata.EventId), edition.Id, 2);
            Reader.Append(edition.Stream, [activated]);
            var confirmed = DomainEventSeed.Attach(new EditionLabelConfirmed(Header.EditionLabel, Header.EditionId, Operation,
                1, activated.Metadata.EventId), label.Id, 2);
            Reader.Append(label.Stream, [confirmed]);
            var drafted = DomainEventSeed.Attach(new CriteriaEditionDrafted(Header.EditionId, Operation, Operation,
                "tsc", Header.EditionLabel, 1, confirmed.Metadata.EventId), edition.Id, 3);
            Reader.Append(edition.Stream, [drafted]);
        }
    }

    sealed class MemoryReader : IAggregateReader
    {
        readonly Dictionary<EventStreamAddress, List<DomainEvent>> _events = [];
        readonly Dictionary<Uuid, ulong> _areas = [];
        ulong _nextArea;
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
        {
            new AggregateScenario<TAggregate>(aggregate).Given(Read(aggregate.Stream));
            return ValueTask.FromResult(aggregate);
        }
        public DomainEvent[] Read(EventStreamAddress stream) => _events.TryGetValue(stream, out var values) ? values.ToArray() : [];
        public void Append(EventStreamAddress stream, IEnumerable<DomainEvent> events)
        {
            if (!_events.TryGetValue(stream, out var values))
                _events.Add(stream, values = []);
            foreach (var fact in events)
            {
                values.Add(fact);
                _areas.Add(fact.Metadata.EventId, ++_nextArea);
            }
        }
        public ulong Area(DomainEvent fact) => _areas[fact.Metadata.EventId];
    }

    sealed class MemoryExecutor(MemoryReader reader) : IAggregateExecutor
    {
        public int Executions { get; private set; }
        public bool Ambiguous { get; set; }
        public Action<Aggregate>? BeforeAppend { get; set; }
        public Action<Aggregate>? AfterAppend { get; set; }
        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        {
            Executions++;
            var owner = await reader.HydrateAsync(aggregate, ct);
            var result = operation(owner).Result;
            BeforeAppend?.Invoke(owner);
            if ((ulong)reader.Read(owner.Stream).Length != owner.CommittedStreamPosition)
                throw new EventStreamConcurrencyException("The source head changed.");
            if (result.IsSuccess)
                reader.Append(owner.Stream, new AggregateScenario<TAggregate>(owner).PendingEvents);
            AfterAppend?.Invoke(owner);
            if (Ambiguous)
                throw new IOException("The append acknowledgement was lost.");
            return result;
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate => throw new NotSupportedException();
    }

    sealed class MemorySource(MemoryReader reader) : ICriteriaExactSourceReader
    {
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default)
        {
            var events = reader.Read(stream);
            for (var index = 0; index < events.Length; index++)
                if (events[index].Metadata.EventId == eventId && events[index].Metadata.AggregateId == aggregateId)
                    return ValueTask.FromResult<CriteriaOrderedSourceEvent?>(new(stream, (ulong)index, reader.Area(events[index]), events[index]));
            return ValueTask.FromResult<CriteriaOrderedSourceEvent?>(null);
        }
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var events = reader.Read(stream);
            for (var index = 0; index < events.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new CriteriaOrderedSourceEvent(stream, (ulong)index, reader.Area(events[index]), events[index]);
            }
            await Task.CompletedTask;
        }
    }

    sealed class Head : ICriteriaFreshEditionHead, ICriteriaFreshEntryHead
    {
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Coverage(bool qualified = true) : ICriteriaSourceCutCoverage
    {
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(qualified);
    }

    sealed class Causality : ICriteriaParticipantCausalOrder
    {
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Publications : ICriteriaPublicationAuthority
    {
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Not yet published.", true)));
    }

}
