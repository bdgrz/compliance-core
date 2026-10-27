using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Queries;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationControlVersionReferencePreviewTests
{
    [Fact]
    public async Task ShouldIncludeCurrentApprovedControlImpactGivenApplicationAndSystemInstanceReferences()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, actorId, "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(kv));
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content);
        var events = new InMemoryEventStore();
        var directReference = Reference(tenantId, "application", applicationId, programId,
            controlId, versionId, "AC-01", Uuid.CreateVersion4(), "Payroll");
        var instanceReference = Reference(tenantId, "system_instance", systemInstanceId,
            programId, controlId, versionId, "AC-01", Uuid.CreateVersion4(),
            "Payroll production");
        var controlVersions = new StubControlVersionDirectory(
            [directReference, instanceReference]);
        var instanceConsistency = new SystemInstanceReadConsistency(applications, sourceReader,
            new LegacySystemInstanceSource(applications, events), events);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: instanceConsistency,
            controlVersions: controlVersions,
            controlVersionConsistency: new ApplicationControlVersionReferenceReadConsistency(
                controlVersions, events));

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value.ApprovedControlReferences,
            reference =>
            {
                Assert.Equal("application", reference.SubjectType);
                Assert.Equal(applicationId, reference.GovernedRecordId);
                Assert.Equal(versionId, reference.VersionId);
            },
            reference =>
            {
                Assert.Equal("system_instance", reference.SubjectType);
                Assert.Equal(systemInstanceId, reference.GovernedRecordId);
                Assert.Equal(versionId, reference.VersionId);
            });
        Assert.DoesNotContain("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
        Assert.NotEmpty(result.Value.ImpactDigest);
    }

    [Fact]
    public async Task ShouldKeepApprovedControlImpactPendingGivenTruncatedReferencePage()
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
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(kv));
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content);
        var events = new InMemoryEventStore();
        var version = Reference(tenantId, "application", applicationId, programId,
            controlId, Uuid.CreateVersion4(), "AC-01", Uuid.CreateVersion4(), "Payroll");
        var controlVersions = new StubControlVersionDirectory([version], "next");
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            controlVersions: controlVersions,
            controlVersionConsistency: new ApplicationControlVersionReferenceReadConsistency(
                controlVersions, events));

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.Contains("approved_control_versions_and_lifecycle_impact_over_limit",
            result.Value.PendingContexts);
    }

    static ApplicationControlVersionReferenceView Reference(Uuid tenantId, string subjectType,
        Uuid recordId, Uuid programId, Uuid controlId, Uuid versionId, string identifier,
        Uuid entryId, string subject) => new(tenantId, subjectType, recordId, programId,
        controlId, identifier, versionId, 1, "approved", new DateOnly(2026, 1, 1), null,
        entryId, subject, "The approved control applies to this record.", false, null);

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StubControlVersionDirectory(
        IReadOnlyList<ApplicationControlVersionReferenceView> references,
        string? nextCursor = null) : IApplicationControlVersionReferenceDirectory
    {
        public ValueTask<Page<ApplicationControlVersionReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            var items = references.Where(reference => reference.TenantId == tenantId &&
                reference.SubjectType == subjectType && reference.GovernedRecordId == recordId)
                .Take(limit).ToArray();
            return ValueTask.FromResult(new Page<ApplicationControlVersionReferenceView>(items,
                nextCursor));
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(Aggregate)source);
    }
}
