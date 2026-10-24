using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationChangePreviewTests
{
    readonly ControlProjectionFacts _controlFacts = new();
    [Fact]
    public async Task ShouldHideRestrictedApplicationGivenChangePreviewWithoutGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow, isRestricted: true).IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        var references = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var controls = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient(), _controlFacts.Content);
        var events = new InMemoryEventStore();
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            references, new ApplicationBoundaryReferenceReadConsistency(references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "revise",
                "Payroll", "Run monthly payroll"), new System.Security.Claims.ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("revise")]
    [InlineData("retire")]
    public async Task ShouldReturnVisibleOutgoingRelationshipsAndResolveRelationshipContextGivenChangePreview(
        string changeKind)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var visibleTargetId = Uuid.CreateVersion4();
        var restrictedTargetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Application", "Purpose", null, actorId, "Manager", now)
            .IsSuccess);
        var visibleTarget = new DeclaredApplication(tenantId, visibleTargetId);
        Assert.True(visibleTarget.Declare("Visible target", "Purpose", null, actorId,
            "Manager", now).IsSuccess);
        var restrictedTarget = new DeclaredApplication(tenantId, restrictedTargetId);
        Assert.True(restrictedTarget.Declare("Restricted target", "Restricted", null,
            actorId, "Manager", now, isRestricted: true).IsSuccess);
        var dependsOnId = Uuid.CreateVersion4();
        var removedId = Uuid.CreateVersion4();
        var hiddenTargetRelationshipId = Uuid.CreateVersion4();
        var replacesId = Uuid.CreateVersion4();
        Assert.True(source.AddApplicationRelationship(0, dependsOnId, visibleTargetId,
            "depends_on", actorId, "Manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(1, removedId, visibleTargetId,
            "replaces", actorId, "Manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(2, hiddenTargetRelationshipId,
            restrictedTargetId, "depends_on", actorId, "Manager", now).IsSuccess);
        Assert.True(source.RemoveApplicationRelationship(3, removedId, actorId,
            "Manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(4, replacesId, visibleTargetId,
            "replaces", actorId, "Manager", now).IsSuccess);
        var aggregateReader = new ApplicationMapReader(source, visibleTarget, restrictedTarget);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var incomingRelationships = new FitzApplicationIncomingRelationshipDirectory(kv);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var events = new InMemoryEventStore();
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Application", "Purpose", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: incomingRelationships,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                incomingRelationships, events));
        var request = changeKind == "revise"
            ? new PreviewApplicationChange(tenantId, applicationId, 1, changeKind,
                "Application v2", "Purpose")
            : new PreviewApplicationChange(tenantId, applicationId, 1, changeKind);

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var relationships = result.Value.ApplicationRelationships.ToDictionary(
            static relationship => relationship.RelationshipId);
        Assert.Equal(2, relationships.Count);
        Assert.Equal("depends_on", relationships[dependsOnId].RelationshipType);
        Assert.Equal("replaces", relationships[replacesId].RelationshipType);
        Assert.DoesNotContain(result.Value.ApplicationRelationships,
            relationship => relationship.RelationshipId == removedId ||
                            relationship.RelationshipId == hiddenTargetRelationshipId);
        Assert.Empty(result.Value.IncomingApplicationRelationships);
        Assert.False(result.Value.Complete);
        Assert.Equal(
            ["vendors", "approved_control_versions_and_lifecycle_impact",
                "system_instance_boundary_references", "system_instance_control_draft_references",
                "policies", "evidence_sources", "access_populations", "review_campaigns",
                "open_work", "readiness", "engagements"],
            result.Value.PendingContexts);
        Assert.DoesNotContain("cross_source_manual_reliance", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldReturnVisibleActiveIncomingRelationshipsAndOmitRemovedAndRestrictedSourcesGivenChangePreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var visibleSourceId = Uuid.CreateVersion4();
        var approvedSourceId = Uuid.CreateVersion4();
        var legacySourceId = Uuid.CreateVersion4();
        var restrictedSourceId = Uuid.CreateVersion4();
        var removedSourceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var approverId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var visibleSource = DeclaredApplicationForTest(tenantId, visibleSourceId, actorId, now);
        var approvedSource = DeclaredApplicationForTest(tenantId, approvedSourceId, actorId, now);
        var legacySource = DeclaredApplicationForTest(tenantId, legacySourceId, actorId, now);
        var restrictedSource = new DeclaredApplication(tenantId, restrictedSourceId);
        Assert.True(restrictedSource.Declare("Restricted", "Purpose", null, actorId,
            "Owner", now, isRestricted: true).IsSuccess);
        var removedSource = DeclaredApplicationForTest(tenantId, removedSourceId, actorId, now);
        var aggregateReader = new ApplicationMapReader(target, visibleSource, approvedSource,
            legacySource, restrictedSource, removedSource);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var relationshipDirectory = new FitzApplicationIncomingRelationshipDirectory(kv);
        var relationshipId = RelationshipId(visibleSourceId, "depends_on", targetId);
        var approvedRelationshipId = RelationshipId(approvedSourceId, "replaces", targetId);
        var legacyRelationshipId = Uuid.CreateVersion4();
        var restrictedRelationshipId = RelationshipId(restrictedSourceId, "depends_on", targetId);
        var removedRelationshipId = RelationshipId(removedSourceId, "depends_on", targetId);
        var relationshipEvents = new DomainEvent[]
        {
            new ApplicationRelationshipRecorded(tenantId, relationshipId, visibleSourceId,
                targetId, "depends_on", 1, "active", 1, 1, actorId, "Visible owner", now),
            new ApplicationRelationshipRecorded(tenantId, approvedRelationshipId,
                approvedSourceId, targetId, "replaces", 1, "proposed", 1, 1, actorId,
                "Proposer", now),
            new ApplicationRelationshipApproved(tenantId, approvedRelationshipId,
                approvedSourceId, targetId, 2, 1, 1, new string('a', 64), approverId,
                "Approver", now.AddMinutes(1)),
            new ApplicationRelationshipAdded(tenantId, legacySourceId, legacyRelationshipId,
                1, targetId, "depends_on", actorId, "Legacy owner", now),
            new ApplicationRelationshipRecorded(tenantId, restrictedRelationshipId,
                restrictedSourceId, targetId, "depends_on", 1, "active", 1, 1, actorId,
                "Hidden owner", now),
            new ApplicationRelationshipRecorded(tenantId, removedRelationshipId,
                removedSourceId, targetId, "depends_on", 1, "active", 1, 1, actorId,
                "Former owner", now),
            new ApplicationRelationshipGovernedRemoved(tenantId, removedRelationshipId,
                removedSourceId, targetId, "depends_on", 2, "No longer needed", actorId,
                "Former owner", now.AddMinutes(2)),
        };
        await ProjectRelationshipsAsync(relationshipDirectory, tenantId, relationshipEvents);
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: relationshipDirectory,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                relationshipDirectory, events));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"), actor),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var incoming = result.Value.IncomingApplicationRelationships;
        Assert.Equal(new[] { visibleSourceId, approvedSourceId, legacySourceId }
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal),
            incoming.Select(static item => item.SourceApplicationId));
        var bySource = incoming.ToDictionary(static item => item.SourceApplicationId);
        Assert.Equal(relationshipId, bySource[visibleSourceId].RelationshipId);
        Assert.Equal("depends_on", bySource[visibleSourceId].RelationshipType);
        Assert.Equal(approverId, bySource[approvedSourceId].ActorMemberId);
        Assert.Equal("replaces", bySource[approvedSourceId].RelationshipType);
        Assert.Equal(legacyRelationshipId, bySource[legacySourceId].RelationshipId);
        Assert.DoesNotContain(incoming, item => item.SourceApplicationId == restrictedSourceId ||
            item.SourceApplicationId == removedSourceId);
        Assert.DoesNotContain("application_relationships", result.Value.PendingContexts);
        Assert.DoesNotContain("application_relationships_over_limit",
            result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldMarkRelationshipImpactOverLimitGivenMoreThanTwoHundredIncomingSources()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var sourceIds = Enumerable.Range(0, 201)
            .Select(index => Uuid.CreateVersion5(targetId, $"incoming-source:{index}"))
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var sources = sourceIds.Select(id => DeclaredApplicationForTest(tenantId, id,
            actorId, now)).ToArray();
        var aggregateReader = new ApplicationMapReader([target, .. sources]);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var relationshipDirectory = new FitzApplicationIncomingRelationshipDirectory(kv);
        var relationshipEvents = sourceIds.Select(sourceId => (DomainEvent)
            new ApplicationRelationshipRecorded(tenantId,
                RelationshipId(sourceId, "depends_on", targetId), sourceId, targetId,
                "depends_on", 1, "active", 1, 1, actorId, "Owner", now)).ToArray();
        await ProjectRelationshipsAsync(relationshipDirectory, tenantId, relationshipEvents);
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: relationshipDirectory,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                relationshipDirectory, events));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"), actor),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(sourceIds.Take(200), result.Value.IncomingApplicationRelationships
            .Select(static relationship => relationship.SourceApplicationId));
        Assert.Equal(200, result.Value.IncomingApplicationRelationships.Count);
        Assert.DoesNotContain("application_relationships", result.Value.PendingContexts);
        Assert.Contains("application_relationships_over_limit",
            result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenIncomingPageExceedsRequestedLimit()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var incomingRows = Enumerable.Range(0, 201).Select(index =>
        {
            var sourceId = Uuid.CreateVersion5(targetId, $"incoming-source:{index}");
            return new ApplicationRelationshipView(
                Uuid.CreateVersion5(sourceId, $"depends_on:{targetId}"), targetId,
                "depends_on", actorId, "Owner", now)
            {
                TenantId = tenantId,
                SourceApplicationId = sourceId,
                Revision = 1,
            };
        }).ToArray();
        var sourceApplications = incomingRows.Select(row =>
            DeclaredApplicationForTest(tenantId, row.SourceApplicationId, actorId, now)).ToArray();
        var aggregateReader = new ApplicationMapReader([target, .. sourceApplications]);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var incoming = new StubIncomingRelationshipDirectory(
            new Page<ApplicationRelationshipView>(incomingRows, null));
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: incoming,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                incoming, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenIncomingProjectionBehindSourceAndRecoverAfterCatchup()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var source = DeclaredApplicationForTest(tenantId, sourceId, actorId, now);
        var aggregateReader = new ApplicationMapReader(target, source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var relationshipDirectory = new FitzApplicationIncomingRelationshipDirectory(kv);
        var events = new InMemoryEventStore();
        var relationshipId = RelationshipId(sourceId, "depends_on", targetId);
        DomainEvent recorded = new ApplicationRelationshipRecorded(tenantId, relationshipId,
            sourceId, targetId, "depends_on", 1, "active", 1, 1, actorId, "Owner", now);
        recorded.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), sourceId, 1, now));
        var stream = new EventStreamAddress(tenantId.ToString(), "applications", sourceId.ToString());
        await events.AppendAsync(stream, 0, [recorded]);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var consistency = new ApplicationIncomingRelationshipReadConsistency(
            relationshipDirectory, events);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: relationshipDirectory,
            incomingRelationshipConsistency: consistency);
        var request = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var cursor = events.ReadAsync(
                         EventStreamPattern.ForPattern(tenantId.ToString(), "applications"),
                         EventCursor.Start, CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await relationshipDirectory.BeginAsync(
                new ProjectionBatchContext(new CheckpointIdentity(
                    ApplicationIncomingRelationshipProjector.ProjectorName,
                    EventStreamPattern.ForPattern(tenantId.ToString(), "applications")),
                    ProjectionCheckpoint.Start));
            await relationshipDirectory.ApplyAsync(cursor.Current.Event);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var recovered = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(relationshipId,
            Assert.Single(recovered.Value.IncomingApplicationRelationships).RelationshipId);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenIncomingSourceChangesDuringPreviewRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var source = DeclaredApplicationForTest(tenantId, sourceId, actorId, now);
        var aggregateReader = new ApplicationMapReader(target, source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var relationshipDirectory = new FitzApplicationIncomingRelationshipDirectory(kv);
        var events = new InMemoryEventStore();
        var relationshipId = RelationshipId(sourceId, "depends_on", targetId);
        DomainEvent arriving = new ApplicationRelationshipRecorded(tenantId, relationshipId,
            sourceId, targetId, "depends_on", 1, "active", 1, 1, actorId, "Owner", now);
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), sourceId, 1, now));
        var stream = new EventStreamAddress(tenantId.ToString(), "applications", sourceId.ToString());
        var incoming = new InterleavingIncomingRelationshipDirectory(relationshipDirectory,
            ct => events.AppendAsync(stream, 0, [arriving], ct));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: incoming,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                incoming, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCrossTenantIncomingProjectionRow()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var target = DeclaredApplicationForTest(tenantId, targetId, actorId, now);
        var source = DeclaredApplicationForTest(tenantId, sourceId, actorId, now);
        var aggregateReader = new ApplicationMapReader(target, source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            targetId, "Target", "Purpose", null, actorId, "Owner", now, ApplicationFacts.Application(tenantId, targetId), null));
        var relationshipId = RelationshipId(sourceId, "depends_on", targetId);
        var corruptRow = new ApplicationRelationshipView(relationshipId, targetId,
            "depends_on", actorId, "Owner", now)
        {
            TenantId = Uuid.CreateVersion4(),
            SourceApplicationId = sourceId,
        };
        var incoming = new StubIncomingRelationshipDirectory(
            new Page<ApplicationRelationshipView>([corruptRow], null));
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: incoming,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                incoming, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, targetId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldBoundVisibleRelationshipsAndMarkOverflowGivenMoreThanTwoHundredOutgoingRelationships()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Application", "Purpose", null, actorId, "Manager", now)
            .IsSuccess);
        var targets = new List<DeclaredApplication>();
        var relationshipIds = new HashSet<Uuid>();
        for (var index = 0; index < 250; index++)
        {
            var targetId = Uuid.CreateVersion4();
            var target = new DeclaredApplication(tenantId, targetId);
            Assert.True(target.Declare($"Target {index}", "Purpose", null, actorId,
                "Manager", now).IsSuccess);
            targets.Add(target);
            var relationshipId = Uuid.CreateVersion4();
            relationshipIds.Add(relationshipId);
            Assert.True(source.AddApplicationRelationship(index, relationshipId, targetId,
                "depends_on", actorId, "Manager", now).IsSuccess);
        }
        var aggregateReader = new ApplicationMapReader([source, .. targets]);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var incomingRelationships = new FitzApplicationIncomingRelationshipDirectory(kv);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var events = new InMemoryEventStore();
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Application", "Purpose", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var handler = new PreviewApplicationChangeHandler(aggregateReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(aggregateReader),
            incomingRelationshipDirectory: incomingRelationships,
            incomingRelationshipConsistency: new ApplicationIncomingRelationshipReadConsistency(
                incomingRelationships, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.Value.ApplicationRelationships.Count);
        Assert.Equal(201, targets.Sum(target => aggregateReader.HydrationCount(target.Id)));
        Assert.All(result.Value.ApplicationRelationships, relationship =>
            Assert.Contains(relationship.RelationshipId, relationshipIds));
        Assert.DoesNotContain("application_relationships", result.Value.PendingContexts);
        Assert.Contains("application_relationships_over_limit", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenRelationshipHistoryChangesDuringPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var targetApplicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Application", "Purpose", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Application", "Purpose", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var controls = new ControlReferenceDirectory
        {
            OnListAsync = _ =>
            {
                Assert.True(source.AddApplicationRelationship(0, Uuid.CreateVersion4(),
                    targetApplicationId, "depends_on", actorId, "Manager", now)
                    .IsSuccess);
                return ValueTask.CompletedTask;
            },
        };
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReportCurrentDraftControlReferencesGivenApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationEntryId = Uuid.CreateVersion4();
        var systemInstanceEntryId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        var events = new InMemoryEventStore();
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        DomainEvent control = await _controlFacts.FreezeAsync(new ControlDraftCreatedContent(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"], null,
                [new ControlApplicabilityReference(applicationEntryId, "application", "Payroll",
                    applicationId, "The control applies to payroll.", false),
                new ControlApplicabilityReference(systemInstanceEntryId, "system_instance",
                    "Payroll production", systemInstanceId,
                    "The control applies to payroll production.", false)]),
            actorId, "Manager", now));
        control.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "controls",
            controlId.ToString()), 0, [control]);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "controls"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await controls.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationControlDraftReferencesV1",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "controls")),
                ProjectionCheckpoint.Start));
            await controls.ApplyAsync(control);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value.ControlDraftReferences,
            reference =>
            {
                Assert.Equal("application", reference.SubjectType);
                Assert.Equal(controlId, reference.ControlId);
                Assert.Equal(programId, reference.ProgramId);
                Assert.Equal("AC-01", reference.Identifier);
                Assert.Equal(applicationEntryId, reference.EntryId);
            },
            reference =>
            {
                Assert.Equal("system_instance", reference.SubjectType);
                Assert.Equal(systemInstanceId, reference.GovernedRecordId);
                Assert.Equal(controlId, reference.ControlId);
                Assert.Equal(programId, reference.ProgramId);
                Assert.Equal("AC-01", reference.Identifier);
                Assert.Equal(systemInstanceEntryId, reference.EntryId);
            });
        Assert.Contains("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.Contains("application_relationships", result.Value.PendingContexts);
        Assert.DoesNotContain("system_instance_control_draft_references",
            result.Value.PendingContexts);
        Assert.DoesNotContain("controls", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldMarkSystemInstanceControlReferencesOverLimitGivenTruncatedPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = DeclaredApplicationForTest(tenantId, applicationId, actorId, now);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Purpose", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var controlReference = new ApplicationControlDraftReferenceView(tenantId,
            "system_instance", systemInstanceId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "AC-01", 1, Uuid.CreateVersion4(), "Payroll production", "Current control draft.");
        var controls = new ControlReferenceDirectory
        {
            PageFor = (subjectType, recordId, _, _) => subjectType == "system_instance" &&
                recordId == systemInstanceId
                ? new Page<ApplicationControlDraftReferenceView>([controlReference], "next")
                : new Page<ApplicationControlDraftReferenceView>([], null),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var sourceReader = new SourceReader(source);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events));
        var context = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        controlReference = controlReference with { Rationale = "Revised control impact." };
        var changed = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(changed.IsSuccess);
        Assert.Equal(200, controls.Limit);
        Assert.Equal(controlReference,
            Assert.Single(changed.Value.ControlDraftReferences));
        Assert.Contains("system_instance_control_draft_references_over_limit",
            changed.Value.PendingContexts);
        Assert.DoesNotContain("system_instance_control_draft_references",
            changed.Value.PendingContexts);
        Assert.False(changed.Value.Complete);
        Assert.NotEqual(result.Value.ImpactDigest, changed.Value.ImpactDigest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTransientConflictGivenInvalidSystemInstanceControlReference(
        bool crossTenant)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = DeclaredApplicationForTest(tenantId, applicationId, actorId, now);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Purpose", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var corrupted = new ApplicationControlDraftReferenceView(
            crossTenant ? Uuid.CreateVersion4() : tenantId, "system_instance",
            crossTenant ? systemInstanceId : Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "AC-01", 1, Uuid.CreateVersion4(), "Payroll production", "Current control draft.");
        var controls = new ControlReferenceDirectory
        {
            PageFor = (subjectType, recordId, _, _) => subjectType == "system_instance" &&
                recordId == systemInstanceId
                ? new Page<ApplicationControlDraftReferenceView>([corrupted], null)
                : new Page<ApplicationControlDraftReferenceView>([], null),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var sourceReader = new SourceReader(source);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlSourceAheadOfProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(tenantId.ToString(), "controls", controlId.ToString());
        DomainEvent control = await _controlFacts.FreezeAsync(new ControlDraftCreatedContent(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"], null,
                [new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                    "Payroll", applicationId, "The control applies to payroll.", false)]),
            actorId, "Manager", now));
        control.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        await events.AppendAsync(stream, 0, [control]);
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            references, new ApplicationBoundaryReferenceReadConsistency(references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));
        var request = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "controls"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await controls.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationControlDraftReferencesV1",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "controls")),
                ProjectionCheckpoint.Start));
            await controls.ApplyAsync(control);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var recovered = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(controlId, Assert.Single(recovered.Value.ControlDraftReferences).ControlId);
    }

    [Fact]
    public async Task ShouldMarkControlDraftReferencesOverLimitGivenTruncatedPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(tenantId, applicationId),
            ], "more"),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, controls.Limit);
        Assert.Contains("control_draft_references_over_limit", result.Value.PendingContexts);
        Assert.Single(result.Value.ControlDraftReferences);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenMismatchedControlReferenceProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(Uuid.CreateVersion4(), applicationId),
            ], null),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlSourceChangesDuringPreviewRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var events = new InMemoryEventStore();
        DomainEvent arriving = await _controlFacts.FreezeAsync(new ControlDraftCreatedContent(tenantId, Uuid.CreateVersion4(), controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"]), actorId, "Manager", now));
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(tenantId, applicationId),
            ], null),
            OnListAsync = ct => events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
                "controls", controlId.ToString()), 0, [arriving], ct),
        };
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenSystemInstanceSourceChangesDuringPreviewRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var projected = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(projected, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var events = new InMemoryEventStore();
        DomainEvent arriving = new SystemInstanceRegistered(tenantId, applicationId, instanceId,
            1, "Production", "production", null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, instanceId), null);
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), instanceId, 1, now));
        var applications = new InterleavingApplicationDirectory(projected, ct =>
            events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "system-instances",
                instanceId.ToString()), 0, [arriving], ct));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var controls = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient(), _controlFacts.Content);
        var sourceReader = new SourceReader(source);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldIncludeApplicationAndSystemInstanceBoundariesGivenChangePreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        DomainEvent boundary = new BoundaryDraftCreated(tenantId, boundaryId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new BoundaryContent("Payroll system", "readiness",
                ["security"],
                [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "application",
                    "Payroll", applicationId, "Operations", "In scope", false),
                new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "system_instance",
                    "Payroll production", systemInstanceId, "Operations", "In scope", false)]),
            actorId, "Manager", now);
        await using (var batch = await references.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("ApplicationBoundaryReferencesV2",
                             EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries")),
                         ProjectionCheckpoint.Start)))
        {
            await references.ApplyAsync(boundary);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, new InMemoryEventStore()), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, new InMemoryEventStore()),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, new InMemoryEventStore()),
                new InMemoryEventStore()));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "revise", "Payroll v2",
                "Run payroll", "Finance", "internal"), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Complete);
        Assert.Equal(3, result.Value.Changes.Count);
        Assert.Equal(["name", "owner_reference", "classification"],
            result.Value.Changes.Select(static change => change.Field));
        Assert.Collection(result.Value.BoundaryReferences,
            reference =>
            {
                Assert.Equal("application", reference.SubjectType);
                Assert.Equal(applicationId, reference.GovernedRecordId);
                Assert.Equal(boundaryId, reference.BoundaryId);
            },
            reference =>
            {
                Assert.Equal("system_instance", reference.SubjectType);
                Assert.Equal(systemInstanceId, reference.GovernedRecordId);
                Assert.Equal(boundaryId, reference.BoundaryId);
            });
        Assert.DoesNotContain("system_instance_boundary_references",
            result.Value.PendingContexts);
        Assert.Contains("engagements", result.Value.PendingContexts);
        Assert.Contains("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.DoesNotContain("controls", result.Value.PendingContexts);
        Assert.DoesNotContain("boundary_references_over_limit", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldMarkInstanceBoundaryImpactOverLimitGivenTruncatedPage()
    {
        // Arrange
        ApplicationBoundaryReferenceView? reference = null;
        var boundaries = new BoundaryReferenceDirectory((subjectType, recordId, _, _) =>
            subjectType == "system_instance" && reference?.GovernedRecordId == recordId
                ? new Page<ApplicationBoundaryReferenceView>([reference], "next")
                : new Page<ApplicationBoundaryReferenceView>([], null));
        var scenario = await CreateSystemInstanceBoundaryScenarioAsync(boundaries);
        reference = new ApplicationBoundaryReferenceView(scenario.TenantId, "system_instance",
            scenario.SystemInstanceId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "draft", null, "inclusion",
            "Payroll production", "Operations", "In scope");

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(reference, Assert.Single(result.Value.BoundaryReferences));
        Assert.Contains("boundary_references_over_limit", result.Value.PendingContexts);
        Assert.DoesNotContain("system_instance_boundary_references",
            result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCrossTenantSystemInstanceBoundaryReference()
    {
        // Arrange
        ApplicationBoundaryReferenceView? reference = null;
        var boundaries = new BoundaryReferenceDirectory((subjectType, recordId, _, _) =>
            subjectType == "system_instance" && reference?.GovernedRecordId == recordId
                ? new Page<ApplicationBoundaryReferenceView>([reference], null)
                : new Page<ApplicationBoundaryReferenceView>([], null));
        var scenario = await CreateSystemInstanceBoundaryScenarioAsync(boundaries);
        reference = new ApplicationBoundaryReferenceView(Uuid.CreateVersion4(),
            "system_instance", scenario.SystemInstanceId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            "draft", null, "inclusion", "Payroll production", "Operations", "In scope");

        // Act
        var result = await scenario.Handler.HandleAsync(scenario.Context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectStaleAndLaggedPreviewGivenAuthoritativeApplication()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        var references = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var controls = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient(), _controlFacts.Content);
        var events = new InMemoryEventStore();
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));
        var request = new PreviewApplicationChange(tenantId, applicationId, 1, "retire");

        // Act
        var lagged = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var ready = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        Assert.Null(source.Revise(1, "Payroll v2", "Run payroll", null,
            actorId, "Manager", now));
        var stale = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        var invalid = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request with { ChangeKind = "activate" }, new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(ready.IsSuccess);
        Assert.Empty(ready.Value.Changes);
        var staleError = Assert.IsType<RequestError>(stale.Error);
        Assert.Equal(RequestErrorKind.Conflict, staleError.Kind);
        Assert.False(staleError.IsTransient);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(invalid.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenBoundarySourceAheadOfProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, _controlFacts.Content);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(tenantId.ToString(), "boundaries",
            boundaryId.ToString());
        DomainEvent boundary = new BoundaryDraftCreated(tenantId, boundaryId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new BoundaryContent("Payroll", "readiness", ["security"],
                [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion",
                    "application", "Payroll", applicationId, "Operations", "In scope", false)]),
            actorId, "Manager", now);
        boundary.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), boundaryId,
            1, now));
        await events.AppendAsync(stream, 0, [boundary]);
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(source)),
            new SystemInstanceReadConsistency(applications, new SourceReader(source),
                new LegacySystemInstanceSource(applications, events), events));
        var request = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "boundaries"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await references.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationBoundaryReferencesV2",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries")),
                ProjectionCheckpoint.Start));
            await references.ApplyAsync(boundary);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var recovered = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.Equal(boundaryId, Assert.Single(recovered.Value.BoundaryReferences).BoundaryId);
    }

    static async Task<(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
        PreviewApplicationChangeHandler Handler, RequestContext<PreviewApplicationChange> Context)>
        CreateSystemInstanceBoundaryScenarioAsync(
            IApplicationBoundaryReferenceDirectory boundaries)
    {
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = DeclaredApplicationForTest(tenantId, applicationId, actorId, now);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content);
        var events = new InMemoryEventStore();
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events));
        var context = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());
        return (tenantId, applicationId, systemInstanceId, handler, context);
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory,
        Uuid tenantId, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async Task ProjectRelationshipsAsync(
        FitzApplicationIncomingRelationshipDirectory directory,
        Uuid tenantId, IReadOnlyList<DomainEvent> domainEvents)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(ApplicationIncomingRelationshipProjector.ProjectorName,
                EventStreamPattern.ForPattern(tenantId.ToString(), "applications")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in domainEvents)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static DeclaredApplication DeclaredApplicationForTest(Uuid tenantId, Uuid applicationId,
        Uuid actorId, DateTimeOffset now)
    {
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Application", "Purpose", null, actorId,
            "Owner", now).IsSuccess);
        return application;
    }

    static Uuid RelationshipId(Uuid sourceId, string relationshipType, Uuid targetId) =>
        Uuid.CreateVersion5(sourceId,
            $"application-relationship:{relationshipType}:{targetId}");

    static ApplicationControlDraftReferenceView ControlReference(Uuid tenantId,
        Uuid applicationId) => new(tenantId, "application", applicationId,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "AC-01", 1, Uuid.CreateVersion4(),
        "Payroll", "The draft applies to payroll.");

    sealed class ApplicationMapReader(params DeclaredApplication[] applications)
        : IAggregateReader
    {
        readonly Dictionary<Uuid, DeclaredApplication> _applications = applications.ToDictionary(
            static application => application.Id);
        readonly Dictionary<Uuid, int> _hydrationCounts = [];

        public int HydrationCount(Uuid applicationId) =>
            _hydrationCounts.GetValueOrDefault(applicationId);

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            _hydrationCounts[aggregate.Id] = _hydrationCounts.GetValueOrDefault(aggregate.Id) + 1;
            return ValueTask.FromResult(_applications.TryGetValue(aggregate.Id, out var source)
                ? (TAggregate)(Aggregate)source
                : aggregate);
        }
    }

    sealed class ControlReferenceDirectory : IApplicationControlDraftReferenceDirectory
    {
        public int Limit { get; private set; }
        public Page<ApplicationControlDraftReferenceView> Page { get; init; } = new([], null);
        public Func<CancellationToken, ValueTask>? OnListAsync { get; init; }
        public Func<string, Uuid, int, string?, Page<ApplicationControlDraftReferenceView>>?
            PageFor
        { get; init; }

        public async ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            Limit = limit;
            if (OnListAsync is not null)
                await OnListAsync(ct);
            return PageFor?.Invoke(subjectType, recordId, limit, cursor) ?? Page;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class BoundaryReferenceDirectory(
        Func<string, Uuid, int, string?, Page<ApplicationBoundaryReferenceView>> pageFor)
        : IApplicationBoundaryReferenceDirectory
    {
        public ValueTask<Page<ApplicationBoundaryReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(pageFor(subjectType, recordId, limit, cursor));

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubIncomingRelationshipDirectory(
        Page<ApplicationRelationshipView> page) : IApplicationIncomingRelationshipDirectory
    {
        public ValueTask<Page<ApplicationRelationshipView>> ListActiveIncomingAsync(Uuid tenantId,
            Uuid targetApplicationId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(page);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class InterleavingIncomingRelationshipDirectory(
        IApplicationIncomingRelationshipDirectory inner,
        Func<CancellationToken, ValueTask> beforeListAsync)
        : IApplicationIncomingRelationshipDirectory
    {
        bool _interleaved;

        public async ValueTask<Page<ApplicationRelationshipView>> ListActiveIncomingAsync(
            Uuid tenantId, Uuid targetApplicationId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            if (!_interleaved)
            {
                _interleaved = true;
                await beforeListAsync(ct).ConfigureAwait(false);
            }
            return await inner.ListActiveIncomingAsync(tenantId, targetApplicationId, limit,
                cursor, ct).ConfigureAwait(false);
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);
    }

    sealed class InterleavingApplicationDirectory(IApplicationDirectoryReader inner,
        Func<CancellationToken, ValueTask> onListInstancesAsync) : IApplicationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);

        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => inner.GetAsync(tenantId, applicationId, ct);

        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            inner.ListAsync(tenantId, limit, cursor, ct);

        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            inner.GetRevisionAsync(tenantId, applicationId, revision, ct);

        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            inner.ListRevisionsAsync(tenantId, applicationId, limit, cursor, ct);

        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => inner.GetInstanceAsync(tenantId, instanceId, ct);

        public async ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default)
        {
            await onListInstancesAsync(ct).ConfigureAwait(false);
            return await inner.ListInstancesAsync(tenantId, applicationId, limit, cursor, ct)
                .ConfigureAwait(false);
        }
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
