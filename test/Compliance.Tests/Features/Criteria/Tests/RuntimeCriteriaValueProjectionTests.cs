using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Projectors;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class RuntimeCriteriaValueProjectionTests
{
    [Theory]
    [InlineData("published")]
    [InlineData("abandoned")]
    [InlineData("cancelled")]
    public async Task ShouldExcludeSeedFactsGivenTheyFollowATerminalEditionDecision(string terminal)
    {
        // Arrange
        var fixture = new Fixture(seed: true);
        await using var batch = await fixture.BeginAsync();
        if (terminal == "cancelled")
        {
            await fixture.RequestAsync();
            await fixture.CancelAsync();
        }
        else
        {
            await fixture.DraftAsync();
            if (terminal == "published")
                await fixture.PublishAsync();
            else
                await fixture.AbandonAsync();
        }

        // Act
        await fixture.SeedFactsAsync();
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var store = new ProbeStore(fixture.Client);
        var edition = await store.EditionAsync(fixture.EditionId);
        var disclosure = await store.DisclosureAsync(fixture.EditionId, "security", "partial");

        // Assert
        Assert.NotNull(edition);
        Assert.Null(edition.CoverageNote);
        Assert.Null(disclosure);
    }

    [Fact]
    public async Task ShouldProjectSeedFactsGivenTheyPrecedePublication()
    {
        // Arrange
        var fixture = new Fixture(seed: true);
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        await fixture.SeedFactsAsync();

        // Act
        await fixture.PublishAsync();
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var store = new ProbeStore(fixture.Client);
        var edition = await store.EditionAsync(fixture.EditionId);
        var disclosure = await store.DisclosureAsync(fixture.EditionId, "security", "partial");

        // Assert
        Assert.Equal("seed note", edition!.CoverageNote);
        Assert.Equal("seed disclosure", disclosure!.Note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveEditionContentGivenSummaryOutcome(bool refused)
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("unchanged catalog content");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, fixture.DraftHead, "draft");

        // Act
        await fixture.SummaryOutcomeAsync(refused);
        await fixture.PublishAsync();
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("unchanged catalog content", edition.Value.CoverageNote);
        Assert.Equal("complete", edition.Value.Coverage);
    }

    [Fact]
    public async Task ShouldApplyDraftDecisionBeforePublicationGivenAnObservedActiveDraftHead()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("accepted during draft");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);

        // Act
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, fixture.DraftHead, "draft");
        await fixture.PublishAsync();
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("accepted during draft", edition.Value.CoverageNote);
        Assert.Equal("complete", edition.Value.Coverage);
    }

    [Fact]
    public async Task ShouldExposeOperatorContentRightsGivenAnAcceptedDraftValue()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("Original summaries only.") with { Field = "content_rights" };
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);

        // Act
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, fixture.DraftHead, "draft");
        var note = fixture.FieldIntent("Partial coverage.");
        var noteClaim = await fixture.ClaimAsync(note, 1);
        var noteProposal = await fixture.ProposeAsync(note, 1, noteClaim.Metadata.EventId);
        await fixture.AcceptAsync(note, 1, noteProposal.Metadata.EventId, fixture.DraftHead, "draft");
        var source = fixture.FieldIntent("https://example.test/source") with { Field = "source_url" };
        var sourceClaim = await fixture.ClaimAsync(source, 1);
        var sourceProposal = await fixture.ProposeAsync(source, 1, sourceClaim.Metadata.EventId);
        await fixture.AcceptAsync(source, 1, sourceProposal.Metadata.EventId, fixture.DraftHead, "draft");
        var complete = fixture.FieldIntent("unused") with
        {
            Field = "numbered_criteria_complete",
            TextValue = null,
            BooleanValue = true,
        };
        var completeClaim = await fixture.ClaimAsync(complete, 1);
        var completeProposal = await fixture.ProposeAsync(complete, 1, completeClaim.Metadata.EventId);
        await fixture.AcceptAsync(complete, 1, completeProposal.Metadata.EventId, fixture.DraftHead, "draft");
        await fixture.PublishAsync();
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);
        var detail = await fixture.Reader.GetLegacyEditionAsync(Uuid.CreateVersion4(), fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("Original summaries only.", edition.Value.ContentRights);
        Assert.True(detail.IsSuccess, detail.Error?.Message);
        Assert.Equal("Original summaries only.", detail.Value!.ContentRights);
    }

    [Fact]
    public async Task ShouldWithholdDraftDecisionGivenHeadIsPublicationInProgress()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("must remain pending");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var publishingHead = await fixture.StartPublicationAsync();

        // Act
        var outcome = await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, publishingHead, "draft");
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var row = await new ProbeStore(fixture.Client).EditionAsync(fixture.EditionId);
        var unresolved = await new ProbeStore(fixture.Client).UnresolvedAsync(outcome.Metadata.EventId);

        // Assert
        Assert.NotNull(row);
        Assert.Null(row.CoverageNote);
        Assert.NotNull(unresolved);
        Assert.Equal(publishingHead.EventId, unresolved.EditionEventId);
        Assert.False(unresolved.TerminalMatches);
    }

    [Fact]
    public async Task ShouldWithholdClaimedValueGivenAcceptanceBeforeItsTerminalMarker()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("unproven early acceptance");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var publication = fixture.NewPublication();
        var future = fixture.FutureTerminal(publication);
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, future);
        await fixture.PublishAsync(publication);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldApplyPreCutProposalGivenAcceptanceAfterPublication()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("settled later");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var terminal = await fixture.PublishAsync();
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("complete", edition.Value.Coverage);
        Assert.Equal("settled later", edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldWithholdMetadataGivenAnUnobservedClaim()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("unverified content");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var proposal = await fixture.ProposeAsync(intent, 1, Uuid.CreateVersion4());
        var terminal = await fixture.PublishAsync();
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldWithholdMetadataGivenAMismatchedClaimIdentity()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("not the claimed fact");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, Uuid.CreateVersion4());
        var terminal = await fixture.PublishAsync();
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldKeepLaterGenerationPendingGivenNoVerifiedTargetFence()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("unqualified recovery");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 2, claim.Metadata.EventId);
        var terminal = await fixture.PublishAsync();
        await fixture.AcceptAsync(intent, 2, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldWithholdDisclosureGivenAnUnobservedClaim()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.DisclosureIntent("unverified disclosure");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var proposal = await fixture.ProposeAsync(intent, 1, Uuid.CreateVersion4());
        var terminal = await fixture.PublishAsync();
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();

        // Act
        var row = await new ProbeStore(fixture.Client).DisclosureAsync(fixture.EditionId, "security", "partial");
        var page = await fixture.Reader.ListDisclosuresAsync(Uuid.CreateVersion4(), fixture.EditionId, 25, null);

        // Assert
        Assert.Null(row);
        Assert.True(page.IsSuccess);
        Assert.Equal("pending", page.Value.Coverage);
        Assert.Empty(page.Value.Items);
    }

    [Fact]
    public async Task ShouldWithholdAcceptedValueGivenProposalAfterPublication()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("late value");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var terminal = await fixture.PublishAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);

        // Act
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldWithholdValueGivenAnUnrelatedTerminalEventId()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("wrong cut");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var terminal = await fixture.PublishAsync();

        // Act
        var wrongTerminal = Uuid.CreateVersion4();
        var outcome = await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId,
            terminal with { EventId = wrongTerminal });
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);
        var unresolved = await new ProbeStore(fixture.Client).UnresolvedAsync(outcome.Metadata.EventId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
        Assert.NotNull(unresolved);
        Assert.Equal(outcome.Metadata.EventId, unresolved.OutcomeEventId);
        Assert.Equal(proposal.Metadata.EventId, unresolved.ProposalEventId);
        Assert.Equal(wrongTerminal, unresolved.EditionEventId);
        Assert.Equal(intent, unresolved.Intent);
        Assert.True(unresolved.ClaimMatches);
        Assert.False(unresolved.TerminalMatches);
    }

    [Fact]
    public async Task ShouldWithholdValueGivenTheWrongTerminalResourceOffset()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("wrong source position");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var terminal = await fixture.PublishAsync();

        // Act
        await fixture.AcceptAsync(intent, 1, proposal.Metadata.EventId,
            terminal with { ResourceOffset = terminal.ResourceOffset + 1 });
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("pending", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldDiscardPostCutProposalGivenAnExactRefusal()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("after publication");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var terminal = await fixture.PublishAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);

        // Act
        await fixture.RefuseAsync(intent, 1, proposal.Metadata.EventId, terminal);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var edition = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.True(edition.IsSuccess);
        Assert.Equal("complete", edition.Value.Coverage);
        Assert.Null(edition.Value.CoverageNote);
    }

    [Fact]
    public async Task ShouldSettlePreAbandonmentProposalGivenAnExactAbandonmentRefusal()
    {
        // Arrange
        var fixture = new Fixture();
        var intent = fixture.FieldIntent("abandoned draft value");
        await using var batch = await fixture.BeginAsync();
        await fixture.DraftAsync();
        var claim = await fixture.ClaimAsync(intent, 1);
        var proposal = await fixture.ProposeAsync(intent, 1, claim.Metadata.EventId);
        var terminal = await fixture.AbandonAsync();

        // Act
        await fixture.RefuseAsync(intent, 1, proposal.Metadata.EventId, terminal, "edition_abandoned");
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        await batch.DisposeAsync();
        var row = await new ProbeStore(fixture.Client).EditionAsync(fixture.EditionId);
        var publicView = await fixture.Reader.GetEditionAsync(fixture.EditionId);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("abandoned", row.Status);
        Assert.Equal(0, row.PendingBeforeCut);
        Assert.Null(row.CoverageNote);
        Assert.False(publicView.IsSuccess);
    }

    sealed class Fixture
    {
        static readonly ActorAttribution Actor = new("operator", "bdgrz");
        static readonly Uuid UserId = Uuid.CreateVersion4();
        static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        readonly CheckpointIdentity _identity = new(RuntimeCriteriaCatalogProjector.WorkloadName,
            EventStreamPattern.ForPattern("bdgrz"));
        readonly CriteriaEdition _edition;
        readonly CriteriaEditionDraftRequested _request;
        readonly Uuid _draft = Uuid.CreateVersion4();
        readonly Dictionary<Uuid, ulong> _proposalAreas = [];
        ulong _cursor;

        public Fixture(bool seed = false)
        {
            _edition = new CriteriaEdition(EditionId);
            _request = Attach(new CriteriaEditionDraftRequested(EditionId, _draft, _draft, 1, "tsc", "edition_1",
                Actor, null, seed ? "seed-v1" : null, seed ? "seed-fingerprint" : null), _edition.Id, 1);
            Projection = new FitzRuntimeCriteriaCatalog(Client);
            Reader = new FitzRuntimeCriteriaCatalog(Client);
        }

        public Uuid EditionId { get; } = Uuid.CreateVersion4();
        public IKvClient Client { get; } = new InMemoryKvClient();
        public FitzRuntimeCriteriaCatalog Projection { get; }
        public FitzRuntimeCriteriaCatalog Reader { get; }
        public Terminal DraftHead { get; private set; }

        public ValueTask<IProjectionBatch> BeginAsync() => Projection.BeginAsync(
            new ProjectionBatchContext(_identity, ProjectionCheckpoint.Start));

        public CriteriaEditionValueIntent FieldIntent(string value) => new(EditionId, _draft, "field", "coverage_note",
            null, null, Uuid.CreateVersion4(), 0, value, null);

        public CriteriaEditionValueIntent DisclosureIntent(string value) => new(EditionId, _draft, "disclosure", null,
            "security", "partial", Uuid.CreateVersion4(), 0, value, null);

        public async Task DraftAsync()
        {
            await RequestAsync();
            var drafted = Attach(new CriteriaEditionDrafted(EditionId, _draft, _draft, "tsc", "edition_1", 1,
                Uuid.CreateVersion4()), _edition.Id, 2);
            DraftHead = new Terminal(drafted.Metadata.EventId, 1, _cursor);
            await EmitAsync(_edition.Stream, drafted, 1);
        }

        public Task RequestAsync() => EmitAsync(_edition.Stream, _request, 0);

        public Task CancelAsync()
        {
            var cancelled = Attach(new CriteriaEditionDraftRequestCancelled(EditionId, Uuid.CreateVersion4(), 1,
                _draft, Actor, null!), _edition.Id, 2);
            return EmitAsync(_edition.Stream, cancelled, 1);
        }

        public async Task SeedFactsAsync()
        {
            var metadata = Attach(new CriteriaSeedMetadataRecorded(EditionId, Uuid.CreateVersion4(), "seed-v1",
                "seed-fingerprint", _request.Metadata.EventId, "tsc", At, "seed note", "https://example.test",
                "seed rights", true, Actor), CriteriaSeedMetadata.IdentityOf(EditionId), 1);
            await EmitAsync(CriteriaSeedMetadata.AddressOf(EditionId), metadata, 0);
            var disclosure = Attach(new CriteriaSeedDisclosureRecorded(EditionId, "security", "partial", Uuid.CreateVersion4(),
                "seed-v1", "seed-fingerprint", _request.Metadata.EventId, "seed disclosure", Actor),
                CriteriaSeedDisclosure.IdentityOf(EditionId, "security", "partial"), 1);
            await EmitAsync(CriteriaSeedDisclosure.AddressOf(EditionId, "security", "partial"), disclosure, 0);
        }

        public async Task<CriteriaEditionValueOperationClaimed> ClaimAsync(CriteriaEditionValueIntent intent, long generation)
        {
            var owner = new CriteriaEditionValueOperation(EditionId, intent.TargetKind, intent.Field, intent.Category,
                intent.Code, intent.OperationId);
            var claim = Attach(new CriteriaEditionValueOperationClaimed(intent, generation, Actor,
                Admission(intent, generation)), owner.Id, 1);
            await EmitAsync(owner.Stream, claim, 0);
            return claim;
        }

        public async Task<CriteriaEditionValueProposed> ProposeAsync(CriteriaEditionValueIntent intent, long generation,
            Uuid claimEventId)
        {
            var owner = Target(intent);
            var proposal = Attach(new CriteriaEditionValueProposed(intent, generation, _request.Metadata.EventId,
                claimEventId, Actor, Admission(intent, generation)), owner.Id, 1);
            _proposalAreas.Add(proposal.Metadata.EventId, _cursor);
            await EmitAsync(owner.Stream, proposal, 0);
            return proposal;
        }

        public async Task<CriteriaEditionValueAccepted> AcceptAsync(CriteriaEditionValueIntent intent, long generation, Uuid proposalEventId,
            Terminal terminal, string basis = "publication")
        {
            var owner = Target(intent);
            var accepted = Attach(new CriteriaEditionValueAccepted(EditionId, intent.OperationId, generation,
                proposalEventId, new CriteriaEditionValueDecisionEvidence(basis, terminal.EventId, terminal.ResourceOffset,
                    terminal.AreaOffset, _proposalAreas[proposalEventId])), owner.Id, 2);
            await EmitAsync(owner.Stream, accepted, 1);
            return accepted;
        }

        public async Task RefuseAsync(CriteriaEditionValueIntent intent, long generation, Uuid proposalEventId,
            Terminal terminal, string reason = "edition_published")
        {
            var owner = Target(intent);
            var refused = Attach(new CriteriaEditionValueRefused(EditionId, intent.OperationId, generation,
                proposalEventId, reason, new CriteriaEditionValueDecisionEvidence(
                    reason == "edition_abandoned" ? "abandonment" : "publication", terminal.EventId,
                    terminal.ResourceOffset, terminal.AreaOffset, _proposalAreas[proposalEventId])), owner.Id, 2);
            await EmitAsync(owner.Stream, refused, 1);
        }

        public async Task SummaryOutcomeAsync(bool refused)
        {
            var owner = new CriteriaEntry(EditionId, "CC6.1");
            var intent = new CriterionSummaryIntent(EditionId, "CC6.1", Uuid.CreateVersion4(), 1, "same text");
            DomainEvent outcome = refused
                ? new CriterionSummaryRefused(intent, 1, Actor, null!, Uuid.CreateVersion4(), 2)
                : new CriterionSummaryUnchanged(intent, 1, Actor, null!, Uuid.CreateVersion4());
            Attach(outcome, owner.Id, 1);
            await EmitAsync(owner.Stream, outcome, 0);
        }

        public CriteriaEditionPublished NewPublication() => Attach(new CriteriaEditionPublished(EditionId, _draft,
            Uuid.CreateVersion4(), 1, "CC6.1", Uuid.CreateVersion4(), "edition_1", 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Actor), _edition.Id, 3);

        public async Task<Terminal> StartPublicationAsync()
        {
            var started = Attach(new CriteriaPublicationStarted(EditionId, _draft, Uuid.CreateVersion4(), 1,
                1, "CC6.1", Uuid.CreateVersion4(), "edition_1", 1, Uuid.CreateVersion4(), Actor, null), _edition.Id, 3);
            var head = new Terminal(started.Metadata.EventId, 2, _cursor);
            await EmitAsync(_edition.Stream, started, 2);
            return head;
        }

        public Terminal FutureTerminal(CriteriaEditionPublished fact) => new(fact.Metadata.EventId, 2, _cursor + 1);

        public async Task<Terminal> PublishAsync(CriteriaEditionPublished? supplied = null)
        {
            var published = supplied ?? NewPublication();
            var terminal = new Terminal(published.Metadata.EventId, 2, _cursor);
            await EmitAsync(_edition.Stream, published, 2);
            return terminal;
        }

        public async Task<Terminal> AbandonAsync()
        {
            var abandoned = Attach(new CriteriaEditionDraftAbandoned(EditionId, _draft, Uuid.CreateVersion4(), 1,
                "edition_1", 1, Actor, null!), _edition.Id, 3);
            var terminal = new Terminal(abandoned.Metadata.EventId, 2, _cursor);
            await EmitAsync(_edition.Stream, abandoned, 2);
            return terminal;
        }

        async Task EmitAsync(EventStreamAddress stream, DomainEvent fact, ulong offset)
        {
            var record = new DomainEventRecord(stream, fact, offset, new EventCursor($"cursor-{++_cursor}"));
            await Projection.ApplyAsync(record, new ProjectionContext(_identity), CancellationToken.None);
        }

        static CriteriaEditionValue Target(CriteriaEditionValueIntent intent) => new(intent.EditionId, intent.TargetKind,
            intent.Field, intent.Category, intent.Code);

        static AccessAdmissionEvidence Admission(CriteriaEditionValueIntent intent, long generation)
        {
            var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
            return new AccessAdmissionEvidence(UserId, Actor.Subject, Actor.Issuer, intent.OperationId,
                Target(intent).Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation),
                At, "policy", source, source)
            {
                FormatVersion = 1,
                PolicyContentSha256 = new string('a', 64),
                Operator = source
            };
        }

        static T Attach<T>(T fact, Uuid ownerId, ulong version) where T : DomainEvent
        {
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), ownerId, version, At));
            return fact;
        }
    }

    readonly record struct Terminal(Uuid EventId, ulong ResourceOffset, ulong AreaOffset);

    sealed record ProjectionContext(CheckpointIdentity Identity) : IProjectorContext
    {
        public bool IsRebuild => Identity.RebuildId is not null;
    }

    sealed class ProbeStore(IKvClient client) : FitzKvProjectionStore(client,
        "kv://bdgrz/runtime-criteria-catalog-v1/projection", RuntimeCriteriaCatalogProjector.WorkloadName)
    {
        public async Task<RuntimeCriteriaDisclosureIndexRow?> DisclosureAsync(Uuid editionId, string category, string code)
        {
            await using var tx = await BeginReadAsync("bdgrz", CancellationToken.None);
            return await RuntimeCriteriaProjectionSchema.Disclosures.GetAsync(tx,
                new RuntimeCriteriaProjectionSchema.DisclosureKey("live", editionId, category, code));
        }

        public async Task<RuntimeCriteriaEditionIndexRow?> EditionAsync(Uuid editionId)
        {
            await using var tx = await BeginReadAsync("bdgrz", CancellationToken.None);
            return await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
                new RuntimeCriteriaProjectionSchema.EditionKey("live", editionId));
        }

        public async Task<RuntimeCriteriaValueUnresolvedIndexRow?> UnresolvedAsync(Uuid outcomeId)
        {
            await using var tx = await BeginReadAsync("bdgrz", CancellationToken.None);
            return await RuntimeCriteriaProjectionSchema.UnresolvedValues.GetAsync(tx,
                new RuntimeCriteriaProjectionSchema.ValueOutcomeKey("live", outcomeId));
        }
    }

}
