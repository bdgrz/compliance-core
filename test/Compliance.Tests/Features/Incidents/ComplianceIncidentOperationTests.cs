using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Tests.Features.Changes;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using ComplianceIncidentAggregate = Bdgrz.Compliance.Features.Incidents.ComplianceIncident;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

public sealed class ComplianceIncidentOperationTests
{
    static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAppendNothingGivenExactRetryOfAssessAfterTheClockMoved()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.RecordedAsync();
        var request = new AssessComplianceIncident(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            incident.Id, incident.Revision, Assessment([]));
        var handler = new AssessComplianceIncidentHandler(host.Operations, host.Content, host.Clock);
        var first = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var after = await host.HistoryAsync(incident);

        // Act
        host.Clock.Advance(TimeSpan.FromHours(3));
        var retry = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(after.Count, (await host.HistoryAsync(incident)).Count);
        Assert.IsType<ComplianceIncidentImpactAssessed>(after[^1]);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedIntentUnderTheSameAssessCommandId()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.RecordedAsync();
        var request = new AssessComplianceIncident(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            incident.Id, incident.Revision, Assessment([]));
        var handler = new AssessComplianceIncidentHandler(host.Operations, host.Content, host.Clock);
        await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var count = (await host.HistoryAsync(incident)).Count;

        // Act
        var changed = await handler.HandleAsync(Host.As(host.AssessorUserId, request with
        {
            Content = Assessment([]) with { Rationale = "A different rationale for the same command" },
        }), CancellationToken.None);
        var otherActor = await handler.HandleAsync(Host.As(Uuid.CreateVersion4(), request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error!.Kind);
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionGivenNewAssessCommandId()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.RecordedAsync();
        var handler = new AssessComplianceIncidentHandler(host.Operations, host.Content, host.Clock);

        // Act
        var stale = await handler.HandleAsync(Host.As(host.AssessorUserId, new AssessComplianceIncident(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, incident.Id, incident.Revision + 5,
            Assessment([]))), CancellationToken.None);
        var negative = await handler.HandleAsync(Host.As(host.AssessorUserId, new AssessComplianceIncident(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, incident.Id, -1, Assessment([]))),
            CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, negative.Error!.Kind);
        Assert.Single(await host.HistoryAsync(incident));
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactRetryOfReviseAfterTheClockMoved()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.RecordedAsync();
        var revised = host.Content1 with { Description = "A credential was exposed through a build log and revoked" };
        var request = new ReviseComplianceIncident(Uuid.CreateVersion4(), host.TenantId, host.ProgramId, incident.Id,
            incident.Revision, revised, "Added the build log detail.");
        var handler = new ReviseComplianceIncidentHandler(host.Operations, host.Content, host.Clock);
        var first = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var count = (await host.HistoryAsync(incident)).Count;

        // Act
        host.Clock.Advance(TimeSpan.FromMinutes(7));
        var retry = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var changed = await handler.HandleAsync(Host.As(host.AssessorUserId, request with
        {
            ChangeReason = "A different reason",
        }), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
        Assert.IsType<ComplianceIncidentRevised>((await host.HistoryAsync(incident))[^1]);
    }

    [Fact]
    public async Task ShouldResolveGivenVerifiedCorrectiveActionCompletionOfEveryAssessedFollowUp()
    {
        // Arrange
        await using var host = new Host();
        var completed = await host.CompletedActionAsync(host.ProgramId);
        var incident = await host.AssessedAsync([completed.WorkItemId]);
        var handler = new RecordComplianceIncidentResolutionHandler(host.Operations, host.Verifier, host.Content, host.Clock);
        var request = Resolution(host, incident, [completed.Fact]);

        // Act
        var resolved = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var saved = await host.Reader.HydrateAsync(new ComplianceIncidentAggregate(host.TenantId, host.ProgramId,
            incident.Id));

        // Assert
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal("resolved", saved.Current!.Status);
        var completion = Assert.Single(saved.Current.Resolution!.FollowUpCompletions);
        Assert.Equal(completed.WorkItemId, completion.WorkItemId);
        Assert.Equal(completed.Fact.EventId, completion.Proof.EventId);
        Assert.Equal(completed.Fact.Stream, completion.Proof.Source);
        Assert.Equal(completed.Fact.AggregateVersion, completion.Proof.OwnerRevision);
        Assert.Equal(completed.WorkItemId, Assert.Single(saved.Current.Resolution.ResolvedFollowUpWorkIds));
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactRetryOfResolveAfterTheClockMoved()
    {
        // Arrange
        await using var host = new Host();
        var completed = await host.CompletedActionAsync(host.ProgramId);
        var incident = await host.AssessedAsync([completed.WorkItemId]);
        var handler = new RecordComplianceIncidentResolutionHandler(host.Operations, host.Verifier, host.Content, host.Clock);
        var request = Resolution(host, incident, [completed.Fact]);
        var first = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var count = (await host.HistoryAsync(incident)).Count;

        // Act
        host.Clock.Advance(TimeSpan.FromHours(1));
        var retry = await handler.HandleAsync(Host.As(host.AssessorUserId, request), CancellationToken.None);
        var changed = await handler.HandleAsync(Host.As(host.AssessorUserId, request with
        {
            Content = request.Content with { Resolution = "A different resolution under the same command" },
        }), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
        Assert.IsType<IncidentResolutionRecorded>((await host.HistoryAsync(incident))[^1]);
    }

    [Fact]
    public async Task ShouldRequireAuthoritativeCompletionProofGivenAssessedFollowUpWork()
    {
        // Arrange
        await using var host = new Host();
        var completed = await host.CompletedActionAsync(host.ProgramId);
        var incident = await host.AssessedAsync([completed.WorkItemId]);
        var handler = new RecordComplianceIncidentResolutionHandler(host.Operations, host.Verifier, host.Content, host.Clock);
        var count = (await host.HistoryAsync(incident)).Count;
        var wrongKind = completed.Raised;
        var wrongStream = completed.Fact with
        {
            Stream = new EventStreamAddress(host.TenantId.ToString(), CorrectiveAction.Area,
                Uuid.CreateVersion4().ToString()),
        };
        var wrongTenant = completed.Fact with
        {
            Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), CorrectiveAction.Area,
                completed.Fact.Stream.Resource),
        };
        var forgedEvent = completed.Fact with { EventId = Uuid.CreateVersion4() };
        var missingPosition = completed.Fact with { AggregateVersion = 99 };
        var otherArea = completed.Fact with
        {
            Stream = new EventStreamAddress(host.TenantId.ToString(), "compliance-incidents", incident.Id.ToString()),
        };

        // Act
        var results = new List<Result>();
        foreach (var facts in new[]
                 {
                     (IReadOnlyList<NativeFactReference>)[],
                     [wrongKind],
                     [wrongStream],
                     [wrongTenant],
                     [forgedEvent],
                     [missingPosition],
                     [otherArea],
                     [completed.Fact, completed.Fact],
                 })
            results.Add(await handler.HandleAsync(Host.As(host.AssessorUserId,
                Resolution(host, incident, facts)), CancellationToken.None));

        // Assert
        Assert.All(results, result => Assert.False(result.IsSuccess));
        Assert.Equal(RequestErrorKind.Conflict, results[0].Error!.Kind);
        Assert.All(results.Skip(1), result => Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind));
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
    }

    [Fact]
    public async Task ShouldRejectCompletionOfAnotherProgramGivenFindingOutsideTheIncidentProgram()
    {
        // Arrange
        await using var host = new Host();
        var foreign = await host.CompletedActionAsync(Uuid.CreateVersion4());
        var incident = await host.AssessedAsync([foreign.WorkItemId]);
        var handler = new RecordComplianceIncidentResolutionHandler(host.Operations, host.Verifier, host.Content, host.Clock);

        // Act
        var result = await handler.HandleAsync(Host.As(host.AssessorUserId,
            Resolution(host, incident, [foreign.Fact])), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(2, (await host.HistoryAsync(incident)).Count);
    }

    [Fact]
    public async Task ShouldRejectResolutionGivenCompletionOfWorkThatWasNotAssessed()
    {
        // Arrange
        await using var host = new Host();
        var completed = await host.CompletedActionAsync(host.ProgramId);
        var incident = await host.AssessedAsync([Uuid.CreateVersion4()]);
        var handler = new RecordComplianceIncidentResolutionHandler(host.Operations, host.Verifier, host.Content, host.Clock);

        // Act
        var result = await handler.HandleAsync(Host.As(host.AssessorUserId,
            Resolution(host, incident, [completed.Fact])), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(2, (await host.HistoryAsync(incident)).Count);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactRetryOfDescriptionDecisionAfterTheClockMoved()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.AssessedAsync([], requiresReview: true);
        var assessment = (await host.Reader.HydrateAsync(new ComplianceIncidentAggregate(host.TenantId, host.ProgramId,
            incident.Id))).Current!.CurrentAssessment!;
        var sha = ComplianceIncidentAggregate.AssessmentSha256(assessment);
        var description = await host.NoChangeDescriptionAsync(incident.Id, sha, assessment.AssessedAt.AddMinutes(5));
        var request = new RecordComplianceIncidentSystemDescriptionDecision(Uuid.CreateVersion4(), host.TenantId,
            host.ProgramId, incident.Id, 1, description.DescriptionId, description.VersionId, description.DecisionId, sha);
        var handler = new RecordComplianceIncidentSystemDescriptionDecisionHandler(host.Operations, host.Reader,
            host.Content, host.Clock);
        var first = await handler.HandleAsync(Host.System(request), CancellationToken.None);
        var count = (await host.HistoryAsync(incident)).Count;

        // Act
        host.Clock.Advance(TimeSpan.FromHours(2));
        var retry = await handler.HandleAsync(Host.System(request), CancellationToken.None);
        var changed = await handler.HandleAsync(Host.System(request with { AssessmentSha256 = new string('e', 64) }),
            CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
        Assert.IsType<ComplianceIncidentSystemDescriptionDecisionRecorded>((await host.HistoryAsync(incident))[^1]);
    }

    [Fact]
    public async Task ShouldTreatDecisionOfSupersededAssessmentAsTerminalGivenNewerIncidentVersion()
    {
        // Arrange
        await using var host = new Host();
        var incident = await host.AssessedAsync([], requiresReview: true);
        var assessment = (await host.Reader.HydrateAsync(new ComplianceIncidentAggregate(host.TenantId, host.ProgramId,
            incident.Id))).Current!.CurrentAssessment!;
        var sha = ComplianceIncidentAggregate.AssessmentSha256(assessment);
        var description = await host.NoChangeDescriptionAsync(incident.Id, sha, assessment.AssessedAt.AddMinutes(5));
        var revise = await new ReviseComplianceIncidentHandler(host.Operations, host.Content, host.Clock).HandleAsync(
            Host.As(host.AssessorUserId, new ReviseComplianceIncident(Uuid.CreateVersion4(), host.TenantId,
                host.ProgramId, incident.Id, 2, host.Content1 with { Title = "Payment API credential exposure" },
                "A newer incident version supersedes the first assessment.")), CancellationToken.None);
        var handler = new RecordComplianceIncidentSystemDescriptionDecisionHandler(host.Operations, host.Reader,
            host.Content, host.Clock);
        var count = (await host.HistoryAsync(incident)).Count;

        // Act
        var result = await handler.HandleAsync(Host.System(new RecordComplianceIncidentSystemDescriptionDecision(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, incident.Id, 1, description.DescriptionId,
            description.VersionId, description.DecisionId, sha)), CancellationToken.None);
        var notVisible = await handler.HandleAsync(Host.System(new RecordComplianceIncidentSystemDescriptionDecision(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, incident.Id, 7, description.DescriptionId,
            description.VersionId, description.DecisionId, sha)), CancellationToken.None);

        // Assert
        Assert.True(revise.IsSuccess, revise.Error?.Message);
        Assert.Equal(Bdgrz.Compliance.Features.Changes.ImpactReactionOutcomes.Superseded, result.Error);
        Assert.Equal(RequestErrorKind.Conflict, notVisible.Error!.Kind);
        Assert.Equal(count, (await host.HistoryAsync(incident)).Count);
    }

    static ComplianceIncidentImpactAssessmentContent Assessment(IReadOnlyList<Uuid> followUpWorkIds,
        bool requiresReview = false) => new("significant", "Reviewed scope, controls, risk, vendors and evidence",
        true, true, true, true,
        requiresReview
            ? ["scope", "control", "risk", "vendor", "evidence", "system_description"]
            : ["scope", "control", "risk", "vendor", "evidence"],
        [new ComplianceIncidentAffectedRecordReference("system_instance", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1)], followUpWorkIds, [Uuid.CreateVersion4()], requiresReview, true, false);

    static RecordComplianceIncidentResolution Resolution(Host host, ComplianceIncidentAggregate incident,
        IReadOnlyList<NativeFactReference> facts) => new(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
        incident.Id, 2, new ComplianceIncidentResolutionContent("The credential was revoked and rotated.",
            Start.AddMinutes(-1), [Uuid.CreateVersion4()], facts));

    sealed class Host : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events = new();
        readonly IAggregateWriter _writer;
        readonly Uuid _reporterMemberId;

        public Host()
        {
            TenantId = Uuid.CreateVersion4();
            ProgramId = Uuid.CreateVersion4();
            AssessorUserId = Uuid.CreateVersion4();
            _reporterMemberId = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(_events);
            services.AddSingleton<IDomainEventReader>(_events);
            services.AddPortia();
            Content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
            services.AddSingleton(Content);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Operations = new NativeOperationExecutor(Reader, _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
                Content);
            Verifier = new FollowUpCompletionVerifier(new EventStoreSourceFactResolver(_events), Content);
            Content1 = new ComplianceIncidentContent(null, null, null, null, "Payment API credential exposed",
                "A credential was briefly exposed and revoked", "security", "high", Start.AddMinutes(-30),
                Start.AddMinutes(-20), RbacIds.Member(TenantId, Uuid.CreateVersion4()), true, false,
                [Uuid.CreateVersion4()], "confidential", [Uuid.CreateVersion4()]);
        }

        public Uuid TenantId { get; }
        public Uuid ProgramId { get; }
        public Uuid AssessorUserId { get; }
        public ComplianceIncidentContent Content1 { get; }
        public IAggregateReader Reader { get; }
        public IImmutableContentStore Content { get; }
        public NativeOperationExecutor Operations { get; }
        public FollowUpCompletionVerifier Verifier { get; }
        public ManualClock Clock { get; } = new(Start);

        public static RequestContext<T> As<T>(Uuid userId, T request) where T : IRequestBase => new(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("name", "Test user")], "UnitTest")));

        public static RequestContext<T> System<T>(T request) where T : IRequestBase => new(request,
            RequestActor.CreateSystem("reactor:compliance-impact-tests", "bdgrz.system"));

        /// <summary>Records an incident through the aggregate so the handlers under test own every later fact.</summary>
        public async Task<ComplianceIncidentAggregate> RecordedAsync()
        {
            var incident = new ComplianceIncidentAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
            Assert.Null(incident.Record(Content1, _reporterMemberId, "Reporter", Start.AddMinutes(-10)));
            await _writer.SaveAsync(incident, new RequestDispatchContext(RequestActor.System));
            return incident;
        }

        /// <summary>An incident assessed through the aggregate, ready for decision or resolution.</summary>
        public async Task<ComplianceIncidentAggregate> AssessedAsync(IReadOnlyList<Uuid> followUpWorkIds,
            bool requiresReview = false)
        {
            var incident = new ComplianceIncidentAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
            Assert.Null(incident.Record(Content1, _reporterMemberId, "Reporter", Start.AddMinutes(-10)));
            Assert.Null(incident.Assess(incident.Revision, Assessment(followUpWorkIds, requiresReview),
                RbacIds.Member(TenantId, AssessorUserId), "Assessor", Start.AddMinutes(-5)));
            await _writer.SaveAsync(incident, new RequestDispatchContext(RequestActor.System));
            return incident;
        }

        public async Task<IReadOnlyList<DomainEvent>> HistoryAsync(ComplianceIncidentAggregate incident)
        {
            var history = new List<DomainEvent>();
            await foreach (var record in _events.ReadAsync(incident.Stream, 0))
                history.Add(record.Event);
            return history;
        }

        /// <summary>A finding whose corrective action was completed; returns the native completion fact.</summary>
        public async Task<Completed> CompletedActionAsync(Uuid programId)
        {
            var findingId = Uuid.CreateVersion4();
            var actionId = Uuid.CreateVersion4();
            var owner = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var actor = ActorReference.ForMember(owner, "Owner");
            await FindingSeed.RaiseAsync(_provider, TenantId, programId, findingId,
                new FindingSource("manual", null, null, "Raised by test"), "Credential exposure follow-up",
                "Rotate the exposed credential.", "high", "Payment API", owner, new DateOnly(2026, 12, 1), [], actor,
                Start.AddMinutes(-9));
            await FindingSeed.AddActionAsync(_provider, TenantId, programId, findingId, actionId,
                "Rotate the credential", owner, new DateOnly(2026, 12, 1), actor, Start.AddMinutes(-4),
                new EvidenceReference(null, "external", "https://example.com/evidence"), owner);
            var finding = new FindingAggregate(TenantId, programId, findingId);
            var action = new CorrectiveAction(TenantId, programId, findingId, actionId);
            NativeFactReference? raised = null;
            NativeFactReference? completion = null;
            await foreach (var record in _events.ReadAsync(finding.Stream, 0))
            {
                var reference = NativeFactReference.Of(finding.Stream, record.Event);
                if (record.Event is FindingRaisedV2)
                    raised = reference;
            }
            await foreach (var record in _events.ReadAsync(action.Stream, 0))
            {
                if (record.Event is CorrectiveActionCompletedV2)
                    completion = NativeFactReference.Of(action.Stream, record.Event);
            }

            return new Completed(Bdgrz.Compliance.Features.Work.WorkCandidate.IdFor(actionId, "corrective_action"),
                completion!, raised!);
        }

        /// <summary>An approved description version recording a no-change decision for the incident.</summary>
        public async Task<DescriptionFact> NoChangeDescriptionAsync(Uuid incidentId, string assessmentSha256,
            DateTimeOffset decidedAt)
        {
            var descriptionId = Uuid.CreateVersion4();
            var versionId = Uuid.CreateVersion4();
            var author = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var decider = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var description = new SystemDescriptionAggregate(TenantId, ProgramId, descriptionId);
            var content = ComplianceImpactReactionHandlerTests.DescriptionContent([]) with
            {
                PeriodChanges = [new SystemDescriptionPeriodChange(incidentId, false, assessmentSha256)],
            };
            Assert.Null(description.Create(versionId, new SystemDescriptionScope("type_ii", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4()), content, author, "Author",
                decidedAt.AddHours(-3)));
            Assert.Null(description.RecordNoChangeDecision(1, incidentId,
                "This incident did not alter the approved system description.", decider, "Decision maker",
                decidedAt));
            await _writer.SaveAsync(description, new RequestDispatchContext(RequestActor.System));
            return new DescriptionFact(descriptionId, versionId,
                Uuid.CreateVersion5(versionId, "no-change:" + incidentId));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed record Completed(Uuid WorkItemId, NativeFactReference Fact, NativeFactReference Raised);

    sealed record DescriptionFact(Uuid DescriptionId, Uuid VersionId, Uuid DecisionId);

    sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
