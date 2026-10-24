using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationControlVersionReferenceDirectoryTests
{
    readonly ControlProjectionFacts _controlFacts = new();
    [Fact]
    public async Task ShouldIndexApprovedVersionsGivenApplicationAndSystemInstanceApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var identity = Identity(tenantId);
        var content = Content(Entry("application", applicationId, "Payroll"),
            Entry("system_instance", instanceId, "Payroll production"));
        await ApplyAsync(directory, identity, await Created(tenantId, programId, controlId, content));

        // Act
        await ApplyAsync(directory, identity, await Approved(tenantId, programId, controlId,
            versionId, 1, content, new DateOnly(2026, 1, 1)));
        var applicationReferences = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);
        var instanceReferences = await directory.ListAsync(tenantId, "system_instance",
            instanceId, 20, null);

        // Assert
        var applicationReference = Assert.Single(applicationReferences.Items);
        Assert.Equal(controlId, applicationReference.ControlId);
        Assert.Equal(programId, applicationReference.ProgramId);
        Assert.Equal(versionId, applicationReference.VersionId);
        Assert.Equal("approved", applicationReference.Status);
        Assert.Equal(new DateOnly(2026, 1, 1), applicationReference.EffectiveFrom);
        Assert.Equal(instanceId, Assert.Single(instanceReferences.Items).GovernedRecordId);
    }

    [Fact]
    public async Task ShouldRetainSupersededVersionAndIndexSuccessorGivenApprovedSuccessor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var predecessorId = Uuid.CreateVersion4();
        var successorId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var identity = Identity(tenantId);
        var originalContent = Content(Entry("application", applicationId, "Payroll"));
        var successorContent = Content(Entry("application", applicationId, "Payroll"));
        await ApplyAsync(directory, identity, await Created(tenantId, programId, controlId,
            originalContent));
        await ApplyAsync(directory, identity, await Approved(tenantId, programId, controlId,
            predecessorId, 1, originalContent, new DateOnly(2026, 1, 1)));

        // Act
        await ApplyAsync(directory, identity, await _controlFacts.FreezeAsync(new ControlSuccessorProposedContent(tenantId,
            programId, controlId, successorId, predecessorId, 2, Uuid.CreateVersion4(),
            "Author", DateTimeOffset.UtcNow)));
        var proposed = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        await ApplyAsync(directory, identity, await Approved(tenantId, programId, controlId,
            successorId, 2, successorContent, new DateOnly(2027, 1, 1), predecessorId));
        var versions = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);

        // Assert
        Assert.True(Assert.Single(proposed.Items).HasPendingSuccessor);
        Assert.Equal(2, versions.Items.Count);
        var predecessor = Assert.Single(versions.Items,
            item => item.VersionId == predecessorId);
        var successor = Assert.Single(versions.Items,
            item => item.VersionId == successorId);
        Assert.Equal("superseded", predecessor.Status);
        Assert.Equal(new DateOnly(2027, 1, 1), predecessor.EffectiveUntil);
        Assert.False(predecessor.HasPendingSuccessor);
        Assert.Equal("approved", successor.Status);
    }

    [Fact]
    public async Task ShouldTrackPendingAndCompletedRetirementGivenRetirementLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var retirementId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var identity = Identity(tenantId);
        var content = Content(Entry("application", applicationId, "Payroll"));
        var effectiveFrom = new DateOnly(2026, 1, 1);
        var effectiveUntil = new DateOnly(2027, 1, 1);
        await ApplyAsync(directory, identity, await Created(tenantId, programId, controlId, content));
        await ApplyAsync(directory, identity, await Approved(tenantId, programId, controlId,
            versionId, 1, content, effectiveFrom));

        // Act
        await ApplyAsync(directory, identity, await _controlFacts.FreezeAsync(new ControlRetirementProposedContent(tenantId,
            programId, controlId, retirementId, versionId, 2, effectiveUntil,
            "The control is no longer required.", Uuid.CreateVersion4(), "Author",
            DateTimeOffset.UtcNow)));
        var pending = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);
        await ApplyAsync(directory, identity, await _controlFacts.FreezeAsync(new ControlProposalWithdrawnContent(tenantId,
            programId, controlId, Uuid.CreateVersion4(), "retirement", retirementId,
            versionId, 2, "Keep the control active.", Uuid.CreateVersion4(),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), DateTimeOffset.UtcNow)));
        var withdrawn = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);
        var finalRetirementId = Uuid.CreateVersion4();
        await ApplyAsync(directory, identity, await _controlFacts.FreezeAsync(new ControlRetirementProposedContent(tenantId,
            programId, controlId, finalRetirementId, versionId, 3, effectiveUntil,
            "Retirement approved.", Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow)));
        await ApplyAsync(directory, identity, await _controlFacts.FreezeAsync(new ControlRetiredContent(tenantId, programId,
            controlId, finalRetirementId, versionId, 3, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), effectiveUntil, "impact-digest", Uuid.CreateVersion4(),
            "Approver", "Retirement approved.", DateTimeOffset.UtcNow)));
        var retired = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);

        // Assert
        Assert.Equal(effectiveUntil, pending.PendingRetirementEffectiveUntil);
        Assert.Null(withdrawn.PendingRetirementEffectiveUntil);
        Assert.Equal("retired", retired.Status);
        Assert.Equal(effectiveUntil, retired.EffectiveUntil);
        Assert.Null(retired.PendingRetirementEffectiveUntil);
    }

    [Fact]
    public async Task ShouldRejectForeignControlEventsGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, await Created(tenantId, programId, controlId,
            Content()));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        var foreignApproval = await Approved(foreignTenantId, programId, controlId,
            Uuid.CreateVersion4(), 1, Content(), new DateOnly(2026, 1, 1));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(foreignApproval));

        // Assert
        Assert.Contains("tenant projection batch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectLaggingAndChangingControlSourceGivenVersionReferenceFence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var events = new InMemoryEventStore();
        var consistency = new ApplicationControlVersionReferenceReadConsistency(directory,
            events);
        var identity = Identity(tenantId);
        var stream = new EventStreamAddress(tenantId.ToString(), "controls",
            controlId.ToString());
        DomainEvent created = await Created(tenantId, programId, controlId, Content());
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1,
            now));
        await events.AppendAsync(stream, 0, [created]);

        // Act
        var lagged = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        await using (var source = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "controls"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await source.MoveNextAsync());
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                identity, ProjectionCheckpoint.Start));
            await directory.ApplyAsync(created);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        DomainEvent revised = await _controlFacts.FreezeAsync(new ControlDraftRevisedContent(tenantId, programId, controlId, 2,
            Content(), Uuid.CreateVersion4(), "Author", now.AddMinutes(1)));
        revised.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 2,
            now.AddMinutes(1)));
        await events.AppendAsync(stream, 1, [revised]);
        var changed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            caughtUp.Value, CancellationToken.None);

        // Assert
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(lagged.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.True(changed.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRollBackAndReplaySuccessorTransitionGivenRejectedWithdrawal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var predecessorId = Uuid.CreateVersion4();
        var successorId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), _controlFacts.Content);
        var identity = Identity(tenantId);
        var content = Content(Entry("application", applicationId, "Payroll"));
        await ApplyAsync(directory, identity, await Created(tenantId, programId, controlId, content));
        await ApplyAsync(directory, identity, await Approved(tenantId, programId, controlId,
            predecessorId, 1, content, new DateOnly(2026, 1, 1)));
        var proposal = await _controlFacts.FreezeAsync(new ControlSuccessorProposedContent(tenantId, programId, controlId,
            successorId, predecessorId, 2, Uuid.CreateVersion4(), "Author",
            DateTimeOffset.UtcNow));

        // Act
        await using (var failedBatch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(proposal);
            var invalidWithdrawal = await _controlFacts.FreezeAsync(new ControlProposalWithdrawnContent(tenantId, programId,
                controlId, Uuid.CreateVersion4(), "successor", Uuid.CreateVersion4(),
                predecessorId, 2, "Wrong proposal.", Uuid.CreateVersion4(),
                ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalidWithdrawal));
        }
        var rolledBack = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);
        await ApplyAsync(directory, identity, proposal);
        var replayed = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);

        // Assert
        Assert.False(rolledBack.HasPendingSuccessor);
        Assert.True(replayed.HasPendingSuccessor);
    }

    Task<ControlDraftCreated> Created(Uuid tenantId, Uuid programId, Uuid controlId,
        ControlDraftContent content) => _controlFacts.FreezeAsync(new ControlDraftCreatedContent(tenantId, programId, controlId,
        Uuid.CreateVersion4(), "AC-01", content, Uuid.CreateVersion4(), "Author",
        DateTimeOffset.UtcNow));

    Task<ControlApproved> Approved(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid versionId, long revision, ControlDraftContent content, DateOnly effectiveFrom,
        Uuid? predecessorVersionId = null) => _controlFacts.FreezeAsync(new ControlApprovedContent(tenantId, programId, controlId, versionId,
        revision, Uuid.CreateVersion4(), Uuid.CreateVersion4(), content,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Approver",
        "Approved for use.", effectiveFrom, DateTimeOffset.UtcNow, null,
        predecessorVersionId));

    static ControlDraftContent Content(params ControlApplicabilityReference[] applicability) =>
        new("Access controls", "Restrict payroll access", "Payroll access control",
            "Administrators use approved access paths.", ["Access review log"], null,
            applicability);

    static ControlApplicabilityReference Entry(string subjectType, Uuid recordId,
        string subject) => new(Uuid.CreateVersion4(), subjectType, subject, recordId,
        "The control applies to this governed record.", false);

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new(FitzApplicationControlVersionReferenceDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls"));

    static async Task ApplyAsync(FitzApplicationControlVersionReferenceDirectory directory,
        CheckpointIdentity identity, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
