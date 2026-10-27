using System.Security.Claims;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Contracts;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Events;
using Bdgrz.Compliance.Features.Incidents.Handlers;
using Bdgrz.Compliance.Features.Incidents.Projectors;
using Bdgrz.Compliance.Features.Incidents.Queries;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Incidents.Tests;

public sealed class ComplianceIncidentDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public async Task ShouldProjectCurrentChronologyAndPageWithinTenantAndProgramGivenReplay()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient(), fixture.Incidents);
        var tenant = fixture.Tenant;
        var program = fixture.Program;
        var foreign = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await RecordAndAssessAsync(fixture, tenant, program, first);
        await RecordAndAssessAsync(fixture, tenant, program, second);
        await RecordAndAssessAsync(fixture, foreign, program, Uuid.CreateVersion4());

        // Act
        await ProjectAsync(directory, fixture, tenant);
        await ProjectAsync(directory, fixture, foreign);
        await ProjectAsync(directory, fixture, tenant);
        var page = await directory.ListAsync(tenant, program, 1, null);
        var next = await directory.ListAsync(tenant, program, 1, page.NextCursor);
        var detail = await directory.GetAsync(tenant, first);
        var wrongTenant = await directory.GetAsync(foreign, first);
        var wrongProgram = await directory.ListAsync(tenant, Uuid.CreateVersion4(), 20, null);
        var get = new GetComplianceIncidentHandler(fixture.Incidents, Visibility(), new(fixture.Reader));
        var result = await get.HandleAsync(Context(new GetComplianceIncident(tenant, program, first)), CancellationToken.None);
        var hidden = await get.HandleAsync(Context(new GetComplianceIncident(tenant, Uuid.CreateVersion4(), first)), CancellationToken.None);
        var list = await new ListComplianceIncidentsHandler(directory, Visibility(), new(directory, fixture.Events)).HandleAsync(
            Context(new ListComplianceIncidents(tenant, program, 1)), CancellationToken.None);

        // Assert
        Assert.Single(page.Items);
        Assert.NotNull(page.NextCursor);
        Assert.Single(next.Items);
        Assert.Equal(new[] { first, second }.Order(), page.Items.Concat(next.Items).Select(item => item.ComplianceIncidentId).Order());
        Assert.All(page.Items.Concat(next.Items), item => { Assert.Equal(tenant, item.TenantId); Assert.Equal(program, item.ProgramId); });
        Assert.Equal(2, detail!.Revision);
        Assert.NotNull(detail.CurrentAssessment);
        Assert.Null(wrongTenant);
        Assert.Empty(wrongProgram.Items);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(first, result.Value.ComplianceIncidentId);
        Assert.Equal(RequestErrorKind.NotFound, hidden.Error!.Kind);
        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Single(list.Value.Items);
    }
    [Fact]
    public async Task ShouldHideRestrictedIncidentFromProgramListAndDetailGivenOrdinaryProgramReader()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient(), fixture.Incidents);
        var visible = Uuid.CreateVersion4();
        var restricted = Uuid.CreateVersion4();
        await RecordAndAssessAsync(fixture, fixture.Tenant, fixture.Program, visible);
        await RecordAndAssessAsync(fixture, fixture.Tenant, fixture.Program, restricted, "restricted");
        await ProjectAsync(directory, fixture, fixture.Tenant);

        // Act
        var list = await new ListComplianceIncidentsHandler(directory, Visibility(), new(directory, fixture.Events)).HandleAsync(
            Context(new ListComplianceIncidents(fixture.Tenant, fixture.Program, 1)), CancellationToken.None);
        var hidden = await new GetComplianceIncidentHandler(fixture.Incidents, Visibility(), new(fixture.Reader)).HandleAsync(
            Context(new GetComplianceIncident(fixture.Tenant, fixture.Program, restricted)), CancellationToken.None);

        // Assert
        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Equal([visible], list.Value.Items.Select(item => item.ComplianceIncidentId));
        Assert.Null(list.Value.NextCursor);
        Assert.Equal(RequestErrorKind.NotFound, hidden.Error!.Kind);
    }
    [Fact]
    public async Task ShouldAllowRestrictedIncidentToItsAssessorGivenAssessorMemberIdentity()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await RecordAndAssessAsync(fixture, fixture.Tenant, fixture.Program, fixture.Incident, "restricted", RbacIds.Member(fixture.Tenant, fixture.User));
        var context = new RequestContext<GetComplianceIncident>(new(fixture.Tenant, fixture.Program, fixture.Incident), Actor(fixture.User));

        // Act
        var result = await new GetComplianceIncidentHandler(fixture.Incidents, Visibility(), new(fixture.Reader)).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.Incident, result.Value.ComplianceIncidentId);
    }
    [Fact]
    public async Task ShouldRejectRevisionGapAndChangedReplayGivenProjectedIncident()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient(), fixture.Incidents);
        var sources = await RecordAndAssessAsync(fixture, fixture.Tenant, fixture.Program, fixture.Incident);
        await ProjectFactsAsync(directory, fixture.Tenant, [sources[0]]);
        var fact = Assert.IsType<ComplianceIncidentImpactAssessed>(sources[1]);

        // Act
        var gap = fact with { Revision = 3, NativeSequence = 3, Header = fact.Header with { Revision = 3 } };
        var gapFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFactsAsync(directory, fixture.Tenant, [gap]));
        await ProjectFactsAsync(directory, fixture.Tenant, [fact]);
        var changed = fact with { Header = fact.Header with { ActorDisplay = "Different actor display" } };
        var replayFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFactsAsync(directory, fixture.Tenant, [changed]));

        // Assert
        Assert.Contains("skipped a native source fact", gapFailure.Message, StringComparison.Ordinal);
        Assert.Contains("conflicting same-sequence authority", replayFailure.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await directory.GetAsync(fixture.Tenant, fixture.Incident))!.Revision);
    }
    [Fact]
    public async Task ShouldServeOnlyCurrentAssessmentGivenIncidentDetailQuery()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var rules = await RecordAndAssessAsync(fixture, fixture.Tenant, fixture.Program, fixture.Incident);
        var request = new GetComplianceImpactAssessment(fixture.Tenant, fixture.Program, fixture.Incident);
        var handler = new GetComplianceImpactAssessmentHandler(fixture.Incidents, Visibility(), new(fixture.Reader));

        // Act
        var result = await handler.HandleAsync(Context(request), CancellationToken.None);
        var foreign = await handler.HandleAsync(Context(request with { ProgramId = Uuid.CreateVersion4() }), CancellationToken.None);
        var before = (await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident)).Rules;
        Assert.Null(before.Revise(before.Revision, before.Current!.Current!.Content with { Title = "Corrected chronology" }, "Correction", Uuid.CreateVersion4(), "Recorder", Now.AddHours(2)));
        await IncidentSeeds.AppendAsync(fixture.Writer, fixture.Incidents, fixture.Content, before);
        var missing = await handler.HandleAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value.ContentVersion);
        Assert.Equal("significant", result.Value.Significance);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, missing.Error!.Kind);
        Assert.Equal(3, (await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident)).View!.Revision);
        Assert.Equal(2, rules.Length);
    }
    static RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request, Actor(Uuid.CreateVersion4()));
    static ClaimsPrincipal Actor(Uuid user) => new(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));
    static RestrictedComplianceRecordVisibility Visibility() => new(new StubPermissions());
    static async Task<DomainEvent[]> RecordAndAssessAsync(IncidentNativeFixture fixture, Uuid tenant, Uuid program, Uuid id, string handling = "confidential", Uuid? assessor = null)
    {
        var rules = new ComplianceIncidentRules(tenant, program, id);
        var source = Uuid.CreateVersion4();
        Assert.Null(rules.Record(new(null, null, null, null, "Production credential alert", "Customer data was briefly exposed.", "security", "high", Now.AddMinutes(-30),
            Now.AddMinutes(-20), Uuid.CreateVersion4(), true, false, [source], handling, []), Uuid.CreateVersion4(), "Recorder", Now));
        Assert.Null(rules.Assess(1, new("significant", "Customer data was affected by the security incident.", true, true, true, true,
            ["scope", "control", "risk", "vendor", "evidence"], [new("system_instance", source, Uuid.CreateVersion4(), 1)], [], [], false, true, false),
            assessor ?? Uuid.CreateVersion4(), "Assessor", Now.AddHours(1)));
        await IncidentSeeds.WriteAsync(fixture.Writer, fixture.Content, rules);
        var result = new List<DomainEvent>();
        await foreach (var record in fixture.Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), ComplianceIncident.Area, id.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result.ToArray();
    }
    static async Task ProjectFactsAsync(FitzComplianceIncidentDirectory directory, Uuid tenant, IReadOnlyList<DomainEvent> facts)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new(ComplianceIncidentDirectoryProjector.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), ComplianceIncident.Area)), ProjectionCheckpoint.Start));
        foreach (var fact in facts)
            await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
    static async Task ProjectAsync(FitzComplianceIncidentDirectory directory, IncidentNativeFixture fixture, Uuid tenant)
    {
        var start = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), ComplianceIncident.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new(ComplianceIncidentDirectoryProjector.ProjectorName, pattern), start));
        var cursor = start.Cursor;
        await foreach (var record in fixture.Events.ReadAsync(pattern, start.Cursor, CancellationToken.None))
        { if (record.Event is IComplianceIncidentFact) await directory.ApplyAsync(record.Event); cursor = record.NextCursor; }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
    sealed class StubPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenant, Uuid user, Uuid member, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
