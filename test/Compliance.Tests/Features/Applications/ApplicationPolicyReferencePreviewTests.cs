using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationPolicyReferencePreviewTests
{
    [Fact]
    public async Task ShouldIncludeApprovedPolicyAndResolvePolicyContextGivenDirectApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
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
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(kv));
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content);
        var policies = PolicyProjectionFacts.ReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var content = new PolicyContent("Access Policy", "Govern payroll access",
            PolicyAudience.CoreSecurity, null, 12, "Policy text", null, "Security lead",
            [new PolicyApplicabilityReference("application", "Payroll", applicationId)]);
        await ProjectPolicyAsync(policies, tenantId, new PolicyDraftCreatedContent(tenantId, programId,
            policyId, Uuid.CreateVersion4(), "POL-01", content, Policy.Hash(content),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), Uuid.CreateVersion4(), now));
        await ProjectPolicyAsync(policies, tenantId, new PolicyApprovedContent(tenantId, programId,
            policyId, 1, 1, true, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Policy.Hash(content), new DateOnly(2026, 1, 1), null, null, "Approved",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Approver"), Uuid.CreateVersion4(),
            now.AddMinutes(1), null));
        var instanceConsistency = new SystemInstanceReadConsistency(applications, sourceReader,
            new LegacySystemInstanceSource(applications, events), events);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: instanceConsistency, policyReferences: policies,
            policyReferenceConsistency: new ApplicationPolicyReferenceReadConsistency(policies,
                events));

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.PolicyReferences);
        Assert.Equal(policyId, reference.PolicyId);
        Assert.Equal(1, reference.Version);
        Assert.Equal("approved", reference.Status);
        Assert.DoesNotContain("policies", result.Value.PendingContexts);
        Assert.DoesNotContain("Policy text", System.Text.Json.JsonSerializer.Serialize(
            result.Value, ComplianceCoreJsonContext.Default.ApplicationChangePreview));
    }

    [Fact]
    public async Task ShouldKeepPolicyContextPendingGivenIndirectApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var policyId = Uuid.CreateVersion4();
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
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(kv));
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv, new ControlProjectionFacts().Content);
        var policies = PolicyProjectionFacts.ReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var content = new PolicyContent("Access Policy", "Govern payroll access",
            PolicyAudience.CoreSecurity, null, 12, "Policy text", null, "Security lead",
            [new PolicyApplicabilityReference("control", "AC-01", Uuid.CreateVersion4())]);
        await ProjectPolicyAsync(policies, tenantId, new PolicyDraftCreatedContent(tenantId, programId,
            policyId, Uuid.CreateVersion4(), "POL-01", content, Policy.Hash(content),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), Uuid.CreateVersion4(), now));
        var instanceConsistency = new SystemInstanceReadConsistency(applications, sourceReader,
            new LegacySystemInstanceSource(applications, events), events);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: instanceConsistency, policyReferences: policies,
            policyReferenceConsistency: new ApplicationPolicyReferenceReadConsistency(policies,
                events));

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.PolicyReferences);
        Assert.Contains("policies", result.Value.PendingContexts);
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async Task ProjectPolicyAsync(FitzApplicationPolicyReferenceDirectory directory,
        Uuid tenantId, IPolicyMutationContent domainEvent)
    {
        var identity = new CheckpointIdentity(
            FitzApplicationPolicyReferenceDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policies"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await PolicyProjectionFacts.ApplyAsync(directory, domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
