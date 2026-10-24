using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationPolicyReferenceDirectoryTests
{
    [Fact]
    public async Task ShouldIndexDirectApplicationAndInstanceReferencesGivenPolicyApproval()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var content = Content(new PolicyApplicabilityReference("application", "Payroll",
                applicationId),
            new PolicyApplicabilityReference("system_instance", "Payroll production", instanceId));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Created(tenantId, programId, policyId, content));

        // Act
        var draftApplication = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var draftInstance = await directory.ListAsync(tenantId, "system_instance", instanceId,
            20, null);
        await ApplyAsync(directory, identity, Approved(tenantId, programId, policyId, 1, 1,
            content, new DateOnly(2026, 1, 1)));
        var application = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var instance = await directory.ListAsync(tenantId, "system_instance", instanceId,
            20, null);
        var unresolved = await directory.HasUnresolvedReferencesAsync(tenantId);

        // Assert
        Assert.Single(draftApplication.Items, reference => reference.Status == "draft");
        Assert.Single(draftInstance.Items, reference => reference.Status == "draft");
        Assert.Equal("approved", Assert.Single(application.Items).Status);
        Assert.Equal(1, application.Items[0].Version);
        Assert.Equal(instanceId, Assert.Single(instance.Items).GovernedRecordId);
        Assert.False(unresolved);
    }

    [Fact]
    public async Task ShouldRetainSupersededPolicyVersionGivenSuccessorApproval()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var original = Content(new PolicyApplicabilityReference("application", "Payroll",
            applicationId));
        var successor = ContentWithTitle("Access v2",
            new PolicyApplicabilityReference("application", "Payroll", applicationId));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Created(tenantId, programId, policyId, original));
        await ApplyAsync(directory, identity, Approved(tenantId, programId, policyId, 1, 1,
            original, new DateOnly(2026, 1, 1)));
        await ApplyAsync(directory, identity, new PolicyDraftRevisedContent(tenantId, programId,
            policyId, 2, successor, Policy.Hash(successor), 1, Actor(), Uuid.CreateVersion4(),
            DateTimeOffset.UtcNow));
        var pending = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items, reference => reference.Version == 1);

        // Act
        await ApplyAsync(directory, identity, Approved(tenantId, programId, policyId, 2, 2,
            successor, new DateOnly(2027, 1, 1), predecessorVersion: 1));
        var versions = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);

        // Assert
        Assert.True(pending.HasPendingSuccessor);
        Assert.Equal(2, versions.Items.Count);
        var predecessor = Assert.Single(versions.Items, reference => reference.Version == 1);
        var current = Assert.Single(versions.Items, reference => reference.Version == 2);
        Assert.Equal("superseded", predecessor.Status);
        Assert.Equal(new DateOnly(2027, 1, 1), predecessor.EffectiveUntil);
        Assert.False(predecessor.HasPendingSuccessor);
        Assert.Equal("approved", current.Status);
        Assert.Equal("Access v2", current.Title);
    }

    [Fact]
    public async Task ShouldTrackRetirementReviewAndCompletionGivenPolicyLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var content = Content(new PolicyApplicabilityReference("application", "Payroll",
            applicationId));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Created(tenantId, programId, policyId, content));
        await ApplyAsync(directory, identity, Approved(tenantId, programId, policyId, 1, 1,
            content, new DateOnly(2026, 1, 1)));

        // Act
        await ApplyAsync(directory, identity, RetirementProposed(tenantId, programId, policyId,
            2, new DateOnly(2027, 1, 1)));
        var proposed = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);
        await ApplyAsync(directory, identity, new PolicyReviewedContent(tenantId, programId, policyId,
            2, Uuid.CreateVersion4(), "request_changes", "Keep it active.", Actor(),
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow, null));
        var withdrawn = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);
        await ApplyAsync(directory, identity, RetirementProposed(tenantId, programId, policyId,
            3, new DateOnly(2027, 1, 1)));
        await ApplyAsync(directory, identity, new PolicyRetiredContent(tenantId, programId, policyId,
            3, 1, Uuid.CreateVersion4(), Uuid.CreateVersion4(), new DateOnly(2027, 1, 1),
            "No longer required.", Actor(), Uuid.CreateVersion4(), DateTimeOffset.UtcNow, null));
        var retired = Assert.Single((await directory.ListAsync(tenantId, "application",
            applicationId, 20, null)).Items);

        // Assert
        Assert.Equal(new DateOnly(2027, 1, 1), proposed.PendingRetirementEffectiveUntil);
        Assert.Null(withdrawn.PendingRetirementEffectiveUntil);
        Assert.Equal("retired", retired.Status);
        Assert.Equal(new DateOnly(2027, 1, 1), retired.EffectiveUntil);
        Assert.Null(retired.PendingRetirementEffectiveUntil);
    }

    [Fact]
    public async Task ShouldKeepPolicyContextUnresolvedGivenIndirectOrUnidentifiedApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var content = Content(new PolicyApplicabilityReference("control", "AC-01",
                Uuid.CreateVersion4()),
            new PolicyApplicabilityReference("application", "Payroll"));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, Identity(tenantId), Created(tenantId, programId, policyId,
            content));
        var direct = await directory.ListAsync(tenantId, "application", applicationId, 20, null);
        var unresolved = await directory.HasUnresolvedReferencesAsync(tenantId);

        // Assert
        Assert.Empty(direct.Items);
        Assert.True(unresolved);
    }

    [Fact]
    public async Task ShouldClearUnresolvedCoverageGivenDraftCorrectionToDirectApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var initial = Content(new PolicyApplicabilityReference("control", "AC-01",
            Uuid.CreateVersion4()));
        var corrected = Content(new PolicyApplicabilityReference("application", "Payroll",
            applicationId));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Created(tenantId, programId, policyId, initial));
        Assert.True(await directory.HasUnresolvedReferencesAsync(tenantId));

        // Act
        await ApplyAsync(directory, identity, new PolicyDraftRevisedContent(tenantId, programId,
            policyId, 2, corrected, Policy.Hash(corrected), null, Actor(),
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow));
        var direct = await directory.ListAsync(tenantId, "application", applicationId, 20, null);
        var unresolved = await directory.HasUnresolvedReferencesAsync(tenantId);

        // Assert
        Assert.Single(direct.Items);
        Assert.False(unresolved);
    }

    [Fact]
    public async Task ShouldRollbackAndReplayPolicyProjectionGivenInvalidApprovalEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var content = Content(new PolicyApplicabilityReference("control", "AC-01",
            Uuid.CreateVersion4()));
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        var created = Created(tenantId, programId, policyId, content);

        // Act
        await using (var failedBatch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await PolicyProjectionFacts.ApplyAsync(directory, created);
            var invalidApproval = Approved(tenantId, programId, policyId, 1, 1,
                Content(new PolicyApplicabilityReference("application", "Other", applicationId)),
                new DateOnly(2026, 1, 1));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await PolicyProjectionFacts.ApplyAsync(directory, invalidApproval));
        }
        var rolledBack = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var unresolvedAfterRollback = await directory.HasUnresolvedReferencesAsync(tenantId);
        await ApplyAsync(directory, identity, created);
        var replayed = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var unresolvedAfterReplay = await directory.HasUnresolvedReferencesAsync(tenantId);

        // Assert
        Assert.Empty(rolledBack.Items);
        Assert.False(unresolvedAfterRollback);
        Assert.Empty(replayed.Items);
        Assert.True(unresolvedAfterReplay);
    }

    [Fact]
    public async Task ShouldReportPolicyProjectionLagGivenUnprojectedSourceWrite()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var content = Content();
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new ApplicationPolicyReferenceReadConsistency(directory, events);
        var identity = Identity(tenantId);
        var stream = new EventStreamAddress(tenantId.ToString(), "policies", policyId.ToString());
        var created = await PolicyProjectionFacts.FreezeAsync(directory, Created(tenantId, programId, policyId, content));
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), policyId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(stream, 0, [created]);

        // Act
        var lagging = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        await using (var source = events.ReadAsync(
                         EventStreamPattern.ForPattern(tenantId.ToString(), "policies"),
                         EventCursor.Start, CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await source.MoveNextAsync());
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            await PolicyProjectionFacts.ApplyAsync(directory, created);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.CaptureAsync(tenantId, CancellationToken.None);
        var reviewed = await PolicyProjectionFacts.FreezeAsync(directory, new PolicyReviewedContent(tenantId, programId, policyId, 1,
            Uuid.CreateVersion4(), "accept", "Reviewed.", Actor(), Uuid.CreateVersion4(),
            DateTimeOffset.UtcNow, null));
        reviewed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), policyId, 2,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(stream, 1, [reviewed]);
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
    public async Task ShouldRejectForeignPolicyEventGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
        var directory = PolicyProjectionFacts.ReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Created(tenantId, Uuid.CreateVersion4(), policyId,
            Content()));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        var foreign = Created(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Content());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await PolicyProjectionFacts.ApplyAsync(directory, foreign));

        // Assert
        Assert.Contains("tenant projection batch", exception.Message, StringComparison.Ordinal);
    }

    static PolicyContent Content(params PolicyApplicabilityReference[] applicability) =>
        ContentWithTitle("Access Policy", applicability);

    static PolicyContent ContentWithTitle(string title,
        params PolicyApplicabilityReference[] applicability) =>
        new(title, "Govern access", PolicyAudience.CoreSecurity, null, 12, "Policy body", null,
            "Security lead", applicability);

    static PolicyDraftCreatedContent Created(Uuid tenantId, Uuid programId, Uuid policyId,
        PolicyContent content) => new(tenantId, programId, policyId, Uuid.CreateVersion4(),
        "POL-01", content, Policy.Hash(content), Actor(), Uuid.CreateVersion4(),
        DateTimeOffset.UtcNow);

    static PolicyApprovedContent Approved(Uuid tenantId, Uuid programId, Uuid policyId, long revision,
        long version, PolicyContent content, DateOnly effectiveFrom,
        long? predecessorVersion = null) => new(tenantId, programId, policyId, revision, version,
        true, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Policy.Hash(content), effectiveFrom,
        predecessorVersion, null, "Approved for use.", Actor(), Uuid.CreateVersion4(),
        DateTimeOffset.UtcNow, null);

    static PolicyRetirementProposedContent RetirementProposed(Uuid tenantId, Uuid programId,
        Uuid policyId, long revision, DateOnly effectiveUntil) => new(tenantId, programId,
        policyId, revision, 1, effectiveUntil, "Retirement proposed.", Actor(),
        Uuid.CreateVersion4(), DateTimeOffset.UtcNow);

    static ActorReference Actor() => ActorReference.ForMember(Uuid.CreateVersion4(), "Member");

    static CheckpointIdentity Identity(Uuid tenantId) => new(
        FitzApplicationPolicyReferenceDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "policies"));

    static async Task ApplyAsync(FitzApplicationPolicyReferenceDirectory directory,
        CheckpointIdentity identity, IPolicyMutationContent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await PolicyProjectionFacts.ApplyAsync(directory, domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
