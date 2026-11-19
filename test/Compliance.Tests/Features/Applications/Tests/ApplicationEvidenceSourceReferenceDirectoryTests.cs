using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Projectors;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationEvidenceSourceReferenceDirectoryTests
{
    [Fact]
    public async Task ShouldIndexControlAndVersionEvidenceSupportsGivenImmutableLinks()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(
            new InMemoryKvClient(), new SupportReader());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Support(tenantId, programId, controlId,
            "control", 2));
        await ApplyAsync(directory, identity, Support(tenantId, programId, controlId,
            "control_version", 1));

        // Act
        var references = await directory.ListByControlAsync(tenantId, controlId, 20, null);
        var unresolved = await directory.HasUnresolvedReferencesAsync(tenantId, [programId]);

        // Assert
        Assert.Equal(2, references.Items.Count);
        Assert.All(references.Items, reference =>
        {
            Assert.Equal(controlId, reference.ControlId);
            Assert.Equal(programId, reference.ProgramId);
            Assert.NotEqual(Uuid.Empty, reference.ArtifactId);
        });
        Assert.False(unresolved);
    }

    [Fact]
    public async Task ShouldKeepEvidenceContextUnresolvedGivenOccurrenceEvidenceSupport()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(
            new InMemoryKvClient(), new SupportReader());

        // Act
        await ApplyAsync(directory, Identity(tenantId), Support(tenantId, programId, controlId,
            "control_occurrence", 1));
        var references = await directory.ListByControlAsync(tenantId, controlId, 20, null);
        var unresolved = await directory.HasUnresolvedReferencesAsync(tenantId, [programId]);
        var unrelatedProgramUnresolved = await directory.HasUnresolvedReferencesAsync(tenantId,
            [Uuid.CreateVersion4()]);

        // Assert
        Assert.Empty(references.Items);
        Assert.True(unresolved);
        Assert.False(unrelatedProgramUnresolved);
    }

    [Fact]
    public async Task ShouldRejectForeignEvidenceSupportGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(
            new InMemoryKvClient(), new SupportReader());
        var identity = Identity(tenantId);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(Support(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), "control", 1)));

        // Assert
        Assert.Contains("tenant projection batch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportEvidenceSupportProjectionLagGivenUnprojectedSourceWrite()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var support = Support(tenantId, programId, controlId, "control", 1);
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(
            new InMemoryKvClient(), new SupportReader());
        var events = new InMemoryEventStore();
        var consistency = new ApplicationEvidenceSourceReferenceReadConsistency(directory,
            events);
        var identity = Identity(tenantId);
        var stream = new EventStreamAddress(tenantId.ToString(), "evidence-support",
            support.SubjectId.ToString());
        DomainEvent domainEvent = support;
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            support.SubjectId, 1, support.OccurredAt));
        await events.AppendAsync(stream, 0, [domainEvent]);

        // Act
        var lagging = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        await using (var source = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "evidence-support"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await source.MoveNextAsync());
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            await directory.ApplyAsync(support);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        var nextSupport = Support(tenantId, programId, controlId, "control", 2);
        DomainEvent nextEvent = nextSupport;
        nextSupport.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            nextSupport.SubjectId, 1, DateTimeOffset.UtcNow.AddMinutes(1)));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "evidence-support",
            nextSupport.SubjectId.ToString()), 0, [nextEvent]);
        var changed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            caughtUp.Value, CancellationToken.None);

        // Assert
        Assert.False(lagging.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagging.Error.Kind);
        Assert.True(lagging.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.True(changed.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRetainImmutableReferenceAndAdvanceCheckpointGivenValidatedWithdrawal()
    {
        // Arrange
        var owner = LinkedOwner("control_version");
        var linked = owner.Current!;
        Assert.Null(owner.Withdraw(new WithdrawEvidenceSupport(Uuid.CreateVersion4(), linked.TenantId,
            linked.ProgramId, linked.SubjectId, 1, "Wrong target version"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), linked.OccurredAt.AddMinutes(1)));
        var withdrawn = owner.Withdrawal!;
        var reader = new SupportReader(owner);
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(new InMemoryKvClient(), reader);
        var identity = Identity(linked.TenantId);
        var events = new InMemoryEventStore();
        await events.AppendAsync(new EventStreamAddress(linked.TenantId.ToString(), "evidence-support",
            linked.SubjectId.ToString()), 0, [linked, withdrawn]);
        var projector = new ApplicationEvidenceSourceReferenceProjector(directory);
        var withdrawalHandler = Assert.IsAssignableFrom<IProjectorHandler<EvidenceSupportWithdrawn>>(projector);

        // Act
        await foreach (var record in events.ReadAsync(identity.Pattern, EventCursor.Start, CancellationToken.None))
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                await directory.LoadCheckpointAsync(linked.TenantId)));
            if (record.Event is EvidenceSupportLinked)
                await directory.ApplyAsync(record.Event);
            else
            {
                var projected = await withdrawalHandler.HandleAsync(new ProjectorContext<EvidenceSupportWithdrawn>(
                    withdrawn, record, new ProjectionContext(identity)), CancellationToken.None);
                Assert.True(projected.IsSuccess, projected.Error?.Message);
            }
            await batch.CommitAsync(new ProjectionCheckpoint(record.NextCursor));
        }
        var references = await directory.ListByControlAsync(linked.TenantId, linked.SupportedRecordId, 20, null);
        var consistency = await new ApplicationEvidenceSourceReferenceReadConsistency(directory, events)
            .CaptureAsync(linked.TenantId, CancellationToken.None);

        // Assert
        var reference = Assert.Single(references.Items);
        Assert.Equal(linked.SubjectId, reference.EvidenceSupportId);
        Assert.Equal(linked.ArtifactId, reference.ArtifactId);
        Assert.Null(reference.SupportStatus);
        Assert.True(consistency.IsSuccess, consistency.Error?.Message);
        Assert.False(await directory.HasUnresolvedReferencesAsync(linked.TenantId, [linked.ProgramId]));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("program")]
    [InlineData("reason")]
    [InlineData("revision")]
    [InlineData("actor")]
    [InlineData("metadata")]
    public async Task ShouldRefuseWithdrawalGivenMissingOrDifferentAuthoritativeLifecycle(string mismatch)
    {
        // Arrange
        var owner = LinkedOwner("control_version");
        var linked = owner.Current!;
        Assert.Null(owner.Withdraw(new WithdrawEvidenceSupport(Uuid.CreateVersion4(), linked.TenantId,
            linked.ProgramId, linked.SubjectId, 1, "Wrong target version"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), linked.OccurredAt.AddMinutes(1)));
        var withdrawal = owner.Withdrawal!;
        var candidate = mismatch switch
        {
            "program" => withdrawal with { ProgramId = Uuid.CreateVersion4() },
            "reason" => withdrawal with { Reason = "Another reason" },
            "revision" => withdrawal with { Revision = 3 },
            "actor" => withdrawal with { ActorMemberId = Uuid.CreateVersion4() },
            _ => withdrawal,
        };
        if (mismatch == "metadata")
        {
            candidate = withdrawal with { };
            candidate.AttachMetadata(withdrawal.Metadata with { EventId = Uuid.CreateVersion4() });
        }
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(new InMemoryKvClient(),
            new SupportReader(mismatch == "missing" ? null : owner));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(linked.TenantId),
            ProjectionCheckpoint.Start));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(candidate));

        // Assert
        Assert.Contains("withdrawal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("area")]
    [InlineData("support")]
    [InlineData("offset")]
    public async Task ShouldRefuseWithdrawalGivenNoncanonicalSourceRecord(string mismatch)
    {
        // Arrange
        var owner = LinkedOwner("control_version");
        var linked = owner.Current!;
        Assert.Null(owner.Withdraw(new WithdrawEvidenceSupport(Uuid.CreateVersion4(), linked.TenantId,
            linked.ProgramId, linked.SubjectId, 1, "Wrong target version"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), linked.OccurredAt.AddMinutes(1)));
        var withdrawal = owner.Withdrawal!;
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(new InMemoryKvClient(), new SupportReader(owner));
        var projector = new ApplicationEvidenceSourceReferenceProjector(directory);
        var handler = Assert.IsAssignableFrom<IProjectorHandler<EvidenceSupportWithdrawn>>(projector);
        var stream = new EventStreamAddress(mismatch == "tenant" ? Uuid.CreateVersion4().ToString() : linked.TenantId.ToString(),
            mismatch == "area" ? "other" : "evidence-support",
            mismatch == "support" ? Uuid.CreateVersion4().ToString() : linked.SubjectId.ToString());
        var source = new DomainEventRecord(stream, withdrawal, mismatch == "offset" ? 0UL : 1UL, EventCursor.Start);
        var context = new ProjectorContext<EvidenceSupportWithdrawn>(withdrawal, source,
            new ProjectionContext(Identity(linked.TenantId)));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(context, CancellationToken.None));

        // Assert
        Assert.Contains("canonical source stream", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRetainUnresolvedHistoryGivenWithdrawnOccurrenceSupport()
    {
        // Arrange
        var owner = LinkedOwner("control_occurrence");
        var linked = owner.Current!;
        Assert.Null(owner.Withdraw(new WithdrawEvidenceSupport(Uuid.CreateVersion4(), linked.TenantId,
            linked.ProgramId, linked.SubjectId, 1, "Wrong occurrence"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), linked.OccurredAt.AddMinutes(1)));
        var directory = new FitzApplicationEvidenceSourceReferenceDirectory(new InMemoryKvClient(), new SupportReader(owner));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(linked.TenantId),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(linked);

        // Act
        await directory.ApplyAsync(owner.Withdrawal!);
        await batch.CommitAsync(ProjectionCheckpoint.Start);

        // Assert
        Assert.True(await directory.HasUnresolvedReferencesAsync(linked.TenantId, [linked.ProgramId]));
        Assert.Empty((await directory.ListByControlAsync(linked.TenantId, linked.SupportedRecordId, 20, null)).Items);
    }

    static EvidenceSupport LinkedOwner(string supportedType)
    {
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var supportId = Uuid.CreateVersion4();
        var owner = new EvidenceSupport(tenantId, programId, supportId);
        var request = new LinkEvidenceSupport(supportId, tenantId, programId, Uuid.CreateVersion4(),
            supportedType, Uuid.CreateVersion4(), 1, new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31), "Quarterly access evidence", "Covers selected control.", 0);
        Assert.Null(owner.Link(request, 1, new string('a', 64),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow));
        return owner;
    }

    sealed class SupportReader(EvidenceSupport? owner = null) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(owner is { } current && current.Id == aggregate.Id
                ? (TAggregate)(Aggregate)current : aggregate);
    }

    sealed record ProjectionContext(CheckpointIdentity Identity) : IProjectorContext
    {
        public bool IsRebuild => Identity.RebuildId is not null;
    }

    static EvidenceSupportLinked Support(Uuid tenantId, Uuid programId, Uuid controlId,
        string supportedType, long supportedRevision) => new(tenantId, Uuid.CreateVersion4(), 1,
        Uuid.CreateVersion4(), DateTimeOffset.UtcNow)
        {
            ProgramId = programId,
            ArtifactId = Uuid.CreateVersion4(),
            ArtifactRevision = 1,
            ArtifactContentSha256 = new string('a', 64),
            SupportedRecordType = supportedType,
            SupportedRecordId = controlId,
            SupportedRecordRevision = supportedRevision,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 1, 31),
            Purpose = "Quarterly access evidence",
            SufficiencyRationale = "Covers the selected access control.",
            ActorDisplay = "Evidence owner",
        };

    static CheckpointIdentity Identity(Uuid tenantId) => new(
        FitzApplicationEvidenceSourceReferenceDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-support"));

    static async Task ApplyAsync(FitzApplicationEvidenceSourceReferenceDirectory directory,
        CheckpointIdentity identity, EvidenceSupportLinked support)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(support);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
