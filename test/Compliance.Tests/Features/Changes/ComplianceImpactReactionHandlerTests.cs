using System.Security.Claims;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Tests.Features.Changes;

public sealed class ComplianceImpactReactionHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 19, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldVerifyWorkOwnershipAndLinkOnlyOnceGivenCurrentChangeAssessment()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var workItemId = Uuid.CreateVersion4();
        var assessor = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var change = AssessedChange(tenantId, programId, changeId, assessor,
            Now.AddHours(-2), [workItemId]);
        await scenario.SaveAsync(change);
        var ownership = new WorkOwnershipReader([workItemId]);
        var handler = new LinkImpactReviewWorkHandler(scenario.Executor, scenario.Reader,
            ownership, new FixedTimeProvider(Now));
        var request = new LinkImpactReviewWork(tenantId, programId, changeId, 1);
        var context = SystemContext(request);

        // Act
        var linked = await handler.HandleAsync(context, CancellationToken.None);
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var saved = await scenario.Reader.HydrateAsync(new SignificantChange(tenantId,
            programId, changeId));

        // Assert
        Assert.True(linked.IsSuccess, linked.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(2, ownership.Calls);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(workItemId, Assert.Single(saved.Current!.ReviewWorkLinks).WorkId);
    }

    [Fact]
    public async Task ShouldRequireExactApprovedSuccessorAndCurrentAssessmentGivenChangeDecision()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var assessor = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var change = AssessedChange(tenantId, programId, changeId, assessor,
            Now.AddHours(-3), []);
        await scenario.SaveAsync(change);
        var descriptionId = Uuid.CreateVersion4();
        var author = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var approver = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var versionId = Uuid.CreateVersion4();
        var description = ApprovedDescription(tenantId, programId, descriptionId,
            versionId, changeId, author, RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            approver);
        await scenario.SaveAsync(description);
        var handler = new RecordSignificantChangeSystemDescriptionDecisionHandler(
            scenario.Executor, scenario.Reader);
        var wrongVersion = new RecordSignificantChangeSystemDescriptionDecision(tenantId,
            programId, changeId, 1, descriptionId, Uuid.CreateVersion4(), versionId,
            "approved_successor", SignificantChange.AssessmentSha256(
                change.Current!.CurrentAssessment!));
        var valid = wrongVersion with { SystemDescriptionVersionId = versionId };
        var wrongAssessment = valid with { AssessmentSha256 = new string('a', 64) };

        // Act
        var rejected = await handler.HandleAsync(SystemContext(wrongVersion), CancellationToken.None);
        var mismatchedAssessment = await handler.HandleAsync(SystemContext(wrongAssessment),
            CancellationToken.None);
        var recorded = await handler.HandleAsync(SystemContext(valid), CancellationToken.None);
        var replayed = await handler.HandleAsync(SystemContext(valid), CancellationToken.None);
        var saved = await scenario.Reader.HydrateAsync(new SignificantChange(tenantId,
            programId, changeId));

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error!.Kind);
        Assert.False(mismatchedAssessment.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, mismatchedAssessment.Error!.Kind);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(3, saved.Revision);
        var decision = Assert.IsType<SignificantChangeSystemDescriptionDecisionView>(
            saved.Current!.CurrentSystemDescriptionDecision);
        Assert.Equal("approved_successor", decision.Decision);
        Assert.Equal(versionId, decision.DecisionId);
        Assert.Equal(approver, decision.DecidedByMemberId);
    }

    [Fact]
    public async Task ShouldCopyExactNoChangeDecisionToCurrentIncidentGivenMatchingSourceIdentity()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var incidentId = Uuid.CreateVersion4();
        var assessor = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var incident = AssessedIncident(tenantId, programId, incidentId, assessor,
            Now.AddHours(-4));
        await scenario.SaveAsync(incident);
        var descriptionId = Uuid.CreateVersion4();
        var author = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var decider = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var versionId = Uuid.CreateVersion4();
        var assessmentSha256 = ComplianceIncident.AssessmentSha256(
            incident.Current!.CurrentAssessment!);
        var description = NoChangeDescription(tenantId, programId, descriptionId,
            versionId, incidentId, author, decider, assessmentSha256);
        await scenario.SaveAsync(description);
        var decisionId = SystemDescriptionDecisionCorrelationId(versionId, incidentId);
        var request = new RecordComplianceIncidentSystemDescriptionDecision(tenantId,
            programId, incidentId, 1, descriptionId, versionId, decisionId,
            assessmentSha256);

        // Act
        var handler = new RecordComplianceIncidentSystemDescriptionDecisionHandler(
            scenario.Executor, scenario.Reader);
        var mismatchedAssessment = await handler.HandleAsync(SystemContext(request with
        {
            AssessmentSha256 = new string('a', 64),
        }), CancellationToken.None);
        var result = await handler.HandleAsync(
            SystemContext(request), CancellationToken.None);
        var replayed = await handler.HandleAsync(
            SystemContext(request), CancellationToken.None);
        var saved = await scenario.Reader.HydrateAsync(new ComplianceIncident(tenantId,
            programId, incidentId));

        // Assert
        Assert.False(mismatchedAssessment.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, mismatchedAssessment.Error!.Kind);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(3, saved.Revision);
        var decision = Assert.IsType<ComplianceIncidentSystemDescriptionDecisionView>(
            saved.Current!.CurrentSystemDescriptionDecision);
        Assert.Equal("no_change", decision.Decision);
        Assert.Equal(decisionId, decision.DecisionId);
        Assert.Equal(decider, decision.DecidedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectNonSystemCallerAndStaleAssessmentGivenInternalDecisionCommands()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var assessor = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var change = AssessedChange(tenantId, programId, changeId, assessor,
            Now.AddHours(-3), []);
        await scenario.SaveAsync(change);
        var descriptionId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        await scenario.SaveAsync(ApprovedDescription(tenantId, programId, descriptionId,
            versionId, changeId, RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            RbacIds.Member(tenantId, Uuid.CreateVersion4())));
        var handler = new RecordSignificantChangeSystemDescriptionDecisionHandler(
            scenario.Executor, scenario.Reader);
        var request = new RecordSignificantChangeSystemDescriptionDecision(tenantId,
            programId, changeId, 2, descriptionId, versionId, versionId,
            "approved_successor", SignificantChange.AssessmentSha256(
                change.Current!.CurrentAssessment!));
        var userContext = new RequestContext<RecordSignificantChangeSystemDescriptionDecision>(
            request, new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())
            ], "UnitTest")));

        // Act
        var unauthorized = await handler.HandleAsync(userContext, CancellationToken.None);
        var stale = await handler.HandleAsync(SystemContext(request), CancellationToken.None);
        var saved = await scenario.Reader.HydrateAsync(new SignificantChange(tenantId,
            programId, changeId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, unauthorized.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Null(saved.Current!.CurrentSystemDescriptionDecision);
    }

    static SignificantChange AssessedChange(Uuid tenantId, Uuid programId, Uuid changeId,
        Uuid assessor, DateTimeOffset assessedAt, IReadOnlyList<Uuid> workIds)
    {
        var change = new SignificantChange(tenantId, programId, changeId);
        Assert.Null(change.Record(new SignificantChangeContent(null, null, null,
            "A production service migration", "Move the service into the approved boundary.",
            new DateOnly(2026, 10, 18), null, RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            [Uuid.CreateVersion4()], "high", "confidential", []),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()), "Recorder", assessedAt.AddMinutes(-1)));
        Assert.Null(change.Assess(change.Revision, new SignificantChangeImpactContent(
            "significant", "The migration affects the approved system description.", true,
            false, false, false, ["scope", "system_description"],
            [new SignificantChangeAffectedRecordReference("system_description_section",
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)], workIds, [], [], [], true),
            assessor, "Assessor", assessedAt));
        return change;
    }

    static ComplianceIncident AssessedIncident(Uuid tenantId, Uuid programId, Uuid incidentId,
        Uuid assessor, DateTimeOffset assessedAt)
    {
        var incident = new ComplianceIncident(tenantId, programId, incidentId);
        var sourceId = Uuid.CreateVersion4();
        Assert.Null(incident.Record(new ComplianceIncidentContent(null, null, null, null,
            "A customer data alert", "Customer data was briefly exposed.", "security", "high",
            assessedAt.AddMinutes(-2), assessedAt.AddMinutes(-1),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()), true, false, [sourceId],
            "confidential", []), RbacIds.Member(tenantId, Uuid.CreateVersion4()), "Recorder",
            assessedAt.AddMinutes(-1)));
        Assert.Null(incident.Assess(incident.Revision,
            new ComplianceIncidentImpactAssessmentContent("significant",
                "Customer data was affected by the incident.", true, true, true, true,
                ["scope", "control", "risk", "vendor", "evidence", "system_description"],
                [new ComplianceIncidentAffectedRecordReference("system_description_section",
                    sourceId, Uuid.CreateVersion4(), 1)], [], [], true, true, false),
            assessor, "Assessor", assessedAt));
        return incident;
    }

    static SystemDescriptionAggregate ApprovedDescription(Uuid tenantId, Uuid programId,
        Uuid descriptionId, Uuid versionId, Uuid changeId, Uuid author, Uuid reviewer,
        Uuid approver)
    {
        var description = new SystemDescriptionAggregate(tenantId, programId, descriptionId);
        Assert.Null(description.Create(versionId, new SystemDescriptionScope("type_i",
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, null),
            DescriptionContent([changeId]), author, "Author", Now.AddHours(-3)));
        Assert.Null(description.Review(1, reviewer, "Reviewer", "approve",
            "The exact version was reviewed.", Now.AddHours(-2)));
        Assert.Null(description.Approve(1, approver, "Approver",
            "The exact successor version is approved.", Now.AddHours(-1)));
        return description;
    }

    static SystemDescriptionAggregate NoChangeDescription(Uuid tenantId, Uuid programId,
        Uuid descriptionId, Uuid versionId, Uuid incidentId, Uuid author, Uuid decider,
        string assessmentSha256)
    {
        var description = new SystemDescriptionAggregate(tenantId, programId, descriptionId);
        var content = DescriptionContent([]) with
        {
            PeriodChanges = [new SystemDescriptionPeriodChange(incidentId, false,
                assessmentSha256)],
        };
        Assert.Null(description.Create(versionId, new SystemDescriptionScope("type_ii",
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4()), content, author, "Author", Now.AddHours(-3)));
        Assert.Null(description.RecordNoChangeDecision(1, incidentId,
            "This incident did not alter the approved system description.", decider,
            "Decision maker", Now.AddHours(-1)));
        return description;
    }

    static SystemDescriptionDraftContent DescriptionContent(IReadOnlyList<Uuid> changeIds) =>
        new("The Sample System", [
            Section("services"),
            Section("commitments_requirements"),
            Section("components_infrastructure_software"),
            Section("people"),
            Section("procedures"),
            Section("information_data_flows"),
            Section("system_boundary"),
            Section("control_environment"),
            Section("user_entity_controls"),
            Section("subservice_organizations"),
            Section("exclusions"),
            Section("relevant_changes"),
        ], [], [], changeIds);

    static SystemDescriptionSection Section(string id) =>
        new(id, id.Replace('_', ' '), "Current system fact.", "complete", null);

    static Uuid SystemDescriptionDecisionCorrelationId(Uuid versionId, Uuid changeId) =>
        Uuid.CreateVersion5(versionId, "no-change:" + changeId);

    static RequestContext<T> SystemContext<T>(T request) where T : IRequestBase =>
        new(request, RequestActor.CreateSystem("reactor:compliance-impact-tests", "bdgrz.system"));

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public Scenario()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }

        public IAggregateExecutor Executor { get; }
        public IAggregateReader Reader { get; }
        IAggregateWriter Writer { get; }

        public ValueTask SaveAsync(Aggregate aggregate) => Writer.SaveAsync(aggregate,
            new RequestDispatchContext(RequestActor.System));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class WorkOwnershipReader(IReadOnlyList<Uuid> expected) : IChangeImpactWorkOwnershipReader
    {
        public int Calls { get; private set; }

        public ValueTask<Result> ConfirmOwnedByProgramAsync(Uuid tenantId, Uuid programId,
            IReadOnlyCollection<Uuid> workItemIds, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(expected.Order().SequenceEqual(workItemIds.Order())
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Work ownership did not match the fixture.")));
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
