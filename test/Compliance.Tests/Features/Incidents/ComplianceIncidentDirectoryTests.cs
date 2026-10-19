using System.Security.Claims;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ComplianceIncidentAggregate = Bdgrz.Compliance.Features.Incidents.ComplianceIncident;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

public sealed class ComplianceIncidentDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectCurrentChronologyAndPageWithinTenantAndProgramGivenReplay()
    {
        // Arrange
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var incidentIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var events = incidentIds.Select(id => RecordAndAssess(tenantId, programId, id)).ToArray();
        var otherTenantEvents = RecordAndAssess(otherTenantId, programId, Uuid.CreateVersion4());

        // Act
        await ProjectAsync(directory, tenantId, events.SelectMany(static batch => batch));
        await ProjectAsync(directory, otherTenantId, otherTenantEvents);
        await ProjectAsync(directory, tenantId, events.SelectMany(static batch => batch));
        var firstPage = await directory.ListAsync(tenantId, programId, 1, null);
        var secondPage = await directory.ListAsync(tenantId, programId, 1, firstPage.NextCursor);
        var detail = await directory.GetAsync(tenantId, incidentIds[0]);
        var wrongTenantDetail = await directory.GetAsync(otherTenantId, incidentIds[0]);
        var wrongProgramPage = await directory.ListAsync(tenantId, Uuid.CreateVersion4(), 20, null);
        var visibility = Visibility();
        var detailResult = await new GetComplianceIncidentHandler(directory, visibility).HandleAsync(
            QueryContext(new GetComplianceIncident(tenantId, programId, incidentIds[0])),
            CancellationToken.None);
        var wrongProgramDetail = await new GetComplianceIncidentHandler(directory, visibility).HandleAsync(
            QueryContext(new GetComplianceIncident(tenantId, Uuid.CreateVersion4(), incidentIds[0])),
            CancellationToken.None);
        var listResult = await new ListComplianceIncidentsHandler(directory, visibility).HandleAsync(
            QueryContext(new ListComplianceIncidents(tenantId, programId, 1)),
            CancellationToken.None);

        // Assert
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Single(secondPage.Items);
        Assert.Equal(incidentIds.Order(), firstPage.Items.Concat(secondPage.Items)
            .Select(static item => item.ComplianceIncidentId).Order());
        Assert.All(firstPage.Items.Concat(secondPage.Items), item =>
        {
            Assert.Equal(tenantId, item.TenantId);
            Assert.Equal(programId, item.ProgramId);
        });
        Assert.Equal(2, detail!.Revision);
        Assert.NotNull(detail.CurrentAssessment);
        Assert.Null(wrongTenantDetail);
        Assert.Empty(wrongProgramPage.Items);
        Assert.True(detailResult.IsSuccess, detailResult.Error?.Message);
        Assert.Equal(incidentIds[0], detailResult.Value.ComplianceIncidentId);
        Assert.Equal(RequestErrorKind.NotFound, wrongProgramDetail.Error!.Kind);
        Assert.True(listResult.IsSuccess, listResult.Error?.Message);
        Assert.Single(listResult.Value.Items);
    }

    [Fact]
    public async Task ShouldHideRestrictedIncidentFromProgramListAndDetailGivenOrdinaryProgramReader()
    {
        // Arrange
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var orderedIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() }
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        var visibleId = orderedIds[0];
        var restrictedId = orderedIds[1];
        var events = RecordAndAssess(tenantId, programId, restrictedId, "restricted")
            .Concat(RecordAndAssess(tenantId, programId, visibleId, "confidential"));
        await ProjectAsync(directory, tenantId, events);
        var visibility = Visibility();
        var list = await new ListComplianceIncidentsHandler(directory, visibility).HandleAsync(
            QueryContext(new ListComplianceIncidents(tenantId, programId, 1)),
            CancellationToken.None);

        // Act
        var restricted = await new GetComplianceIncidentHandler(directory, visibility).HandleAsync(
            QueryContext(new GetComplianceIncident(tenantId, programId, restrictedId)),
            CancellationToken.None);

        // Assert
        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Equal([visibleId], list.Value.Items.Select(static item => item.ComplianceIncidentId));
        Assert.Null(list.Value.NextCursor);
        Assert.Equal(RequestErrorKind.NotFound, restricted.Error!.Kind);
    }

    [Fact]
    public async Task ShouldAllowRestrictedIncidentToItsAssessorGivenAssessorMemberIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var incidentId = Uuid.CreateVersion4();
        var assessorUserId = Uuid.CreateVersion4();
        var assessorMemberId = RbacIds.Member(tenantId, assessorUserId);
        var events = RecordAndAssess(tenantId, programId, incidentId, "restricted", assessorMemberId);
        var directory = new StubDirectory(Assert.IsType<ComplianceIncidentImpactAssessed>(events[^1]).State);
        var context = new RequestContext<GetComplianceIncident>(
            new GetComplianceIncident(tenantId, programId, incidentId), Actor(assessorUserId));

        // Act
        var result = await new GetComplianceIncidentHandler(directory, Visibility())
            .HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(incidentId, result.Value.ComplianceIncidentId);
    }

    [Fact]
    public async Task ShouldRejectRevisionGapAndChangedReplayGivenProjectedIncident()
    {
        // Arrange
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var events = RecordAndAssess(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await ProjectAsync(directory, tenantId, [events[0]]);
        var assessmentEvent = Assert.IsType<ComplianceIncidentImpactAssessed>(events[1]);

        // Act
        var gap = assessmentEvent with
        {
            Revision = 3,
            State = assessmentEvent.State with { Revision = 3 },
        };
        var gapFailure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ProjectAsync(directory, tenantId, [gap]));
        await ProjectAsync(directory, tenantId, [assessmentEvent]);
        var changed = assessmentEvent with
        {
            State = assessmentEvent.State with { ActorDisplay = "Different actor display" },
        };
        var replayFailure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ProjectAsync(directory, tenantId, [changed]));

        // Assert
        Assert.Contains("skipped a revision", gapFailure.Message, StringComparison.Ordinal);
        Assert.Contains("conflicting projection content", replayFailure.Message,
            StringComparison.Ordinal);
        Assert.Equal(2, (await directory.GetAsync(tenantId,
            assessmentEvent.SubjectId))!.Revision);
    }

    [Fact]
    public async Task ShouldServeOnlyCurrentAssessmentGivenIncidentDetailQuery()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var incidentId = Uuid.CreateVersion4();
        var events = RecordAndAssess(tenantId, programId, incidentId);
        var directory = new StubDirectory(
            Assert.IsType<ComplianceIncidentImpactAssessed>(events[^1]).State);
        var request = new GetComplianceImpactAssessment(tenantId, programId, incidentId);
        var context = new RequestContext<GetComplianceImpactAssessment>(request,
            RequestActor.CreateSystem("query-test", "tests"));

        // Act
        var visibility = Visibility();
        var result = await new GetComplianceImpactAssessmentHandler(directory, visibility)
            .HandleAsync(context, CancellationToken.None);
        var wrongProgram = await new GetComplianceImpactAssessmentHandler(directory, visibility)
            .HandleAsync(new RequestContext<GetComplianceImpactAssessment>(request with
            {
                ProgramId = Uuid.CreateVersion4(),
            }, RequestActor.CreateSystem("assessment-query-test", "tests")),
                CancellationToken.None);
        var recorded = Assert.IsType<ComplianceIncidentRecorded>(events[0]);
        var assessmentMissing = await new GetComplianceImpactAssessmentHandler(
                new StubDirectory(recorded.State), visibility)
            .HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value.ContentVersion);
        Assert.Equal("significant", result.Value.Significance);
        Assert.Equal(RequestErrorKind.NotFound, wrongProgram.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, assessmentMissing.Error!.Kind);
    }

    static RequestContext<T> QueryContext<T>(T request) where T : IRequestBase =>
        new(request, RequestActor.CreateSystem("incident-query-test", "tests"));

    static RestrictedComplianceRecordVisibility Visibility() =>
        new(new StubPermissions(false));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    static async Task ProjectAsync(FitzComplianceIncidentDirectory directory, Uuid tenantId,
        IEnumerable<DomainEvent> events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(ComplianceIncidentDirectoryProjector.ProjectorName,
                EventStreamPattern.ForPattern(tenantId.ToString())), ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static DomainEvent[] RecordAndAssess(Uuid tenantId, Uuid programId, Uuid incidentId,
        string handlingClass = "confidential", Uuid? assessorMemberId = null)
    {
        var incident = new ComplianceIncidentAggregate(tenantId, programId, incidentId);
        var actor = Uuid.CreateVersion4();
        var assessor = assessorMemberId ?? Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        Assert.Null(incident.Record(new ComplianceIncidentContent(null, null, null, null,
            "Production credential alert", "Customer data was briefly exposed.", "security",
            "high", Now.AddMinutes(-30), Now.AddMinutes(-20), owner, true, false,
            [sourceId], handlingClass, []), actor, "Recorder", Now));
        Assert.Null(incident.Assess(incident.Revision,
            new ComplianceIncidentImpactAssessmentContent("significant",
                "Customer data was affected by the security incident.", true, true, true, true,
                ["scope", "control", "risk", "vendor", "evidence"],
                [new ComplianceIncidentAffectedRecordReference("system_instance", sourceId,
                    Uuid.CreateVersion4(), 1)], [], [], false, true, false), assessor,
            "Assessor", Now.AddHours(1)));
        return new AggregateScenario<ComplianceIncidentAggregate>(incident).PendingEvents.ToArray();
    }

    sealed class StubDirectory(ComplianceIncidentView? incident) : IComplianceIncidentDirectoryReader
    {
        public ValueTask<ComplianceIncidentView?> GetAsync(Uuid tenantId, Uuid incidentId,
            CancellationToken ct = default) => ValueTask.FromResult(incident is { } view &&
                view.TenantId == tenantId && view.ComplianceIncidentId == incidentId ? view : null);

        public ValueTask<Cntryl.Fitz.Extensions.Page<ComplianceIncidentSummaryView>> ListAsync(
            Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    sealed class StubPermissions(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }
}
