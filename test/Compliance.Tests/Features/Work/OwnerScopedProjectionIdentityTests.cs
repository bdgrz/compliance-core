using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class OwnerScopedProjectionIdentityTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothPendingPlansAndRemoveOnlyApprovedOwnerGivenReusedLocalPlanId(bool sameControl)
    {
        // Arrange
        await using var source = new NativeSources();
        var tenant = Uuid.CreateVersion4();
        var firstProgram = Uuid.CreateVersion4();
        var secondProgram = sameControl ? Uuid.CreateVersion4() : firstProgram;
        var firstControl = Uuid.CreateVersion4();
        var secondControl = sameControl ? firstControl : Uuid.CreateVersion4();
        var shared = Uuid.CreateVersion4();
        var first = await source.ProposePlanAsync(tenant, firstProgram, firstControl, shared);
        var second = await source.ProposePlanAsync(tenant, secondProgram, secondControl, shared);
        var directory = new FitzControlOperatingPlanWorkItemDirectory(source.Kv,
            new ControlReader(source.Reader, source.Events, source.Content), source.Content);
        var rows = new PlanRows(source.Kv);

        // Act
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            tenant, ControlOperatingPlan.Area, await source.FactsAsync(tenant, ControlOperatingPlan.Area));
        var firstPending = await rows.ReadAsync(tenant, firstProgram);
        var secondPending = sameControl ? await rows.ReadAsync(tenant, secondProgram) : firstPending;
        var approval = await source.ApprovePlanAsync(first);
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            tenant, ControlOperatingPlan.Area, [approval]);
        var remainingFirst = await rows.ReadAsync(tenant, firstProgram);
        var remainingSecond = sameControl ? await rows.ReadAsync(tenant, secondProgram) : remainingFirst;

        // Assert
        Assert.Contains(firstPending, row => row.ControlId == firstControl && row.PlanVersionId == shared);
        Assert.Contains(secondPending, row => row.ControlId == secondControl && row.PlanVersionId == shared);
        Assert.Equal(sameControl ? 1 : 2, firstPending.Count);
        Assert.Equal(sameControl ? 0 : 1, remainingFirst.Count);
        var remaining = Assert.Single(remainingSecond);
        Assert.Equal(second.ProgramId, remaining.ProgramId);
        Assert.Equal(second.ControlId, remaining.ControlId);
        Assert.Equal(shared, remaining.PlanVersionId);
    }

    [Fact]
    public async Task ShouldProduceDistinctQueueIdentitiesGivenSamePlanCommandAcceptedByTwoNativeLines()
    {
        // Arrange
        await using var source = new NativeSources();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var shared = Uuid.CreateVersion4();
        var first = await source.ProposePlanAsync(tenant, program, Uuid.CreateVersion4(), shared);
        var second = await source.ProposePlanAsync(tenant, program, Uuid.CreateVersion4(), shared);

        // Act
        var candidates = new[] { first, second }.Select(plan => WorkSource.ControlOperatingPlanApprovalCandidate(
            tenant, program, plan.ControlId, "AC-OPS", shared, plan.ProposerMemberId, plan.ProposedAt)).ToArray();

        // Assert
        Assert.Equal(2, candidates.Select(candidate => candidate.WorkItemId).Distinct().Count());
        Assert.All(candidates, candidate => Assert.Equal(shared, candidate.SourceId));
        Assert.Equal(2, (await source.FactsAsync(tenant, ControlOperatingPlan.Area)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothMappingReviewsAndRemoveOnlyReviewedLineGivenReusedLocalMappingId(bool sameControl)
    {
        // Arrange
        await using var source = new NativeSources();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var firstControl = Uuid.CreateVersion4();
        var secondControl = sameControl ? firstControl : Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var secondCriterion = sameControl ? "CC7.1" : "CC6.1";
        var shared = Uuid.CreateVersion4();
        var first = await source.ProposeMappingAsync(tenant, program, firstControl, edition, "CC6.1", shared);
        _ = await source.ProposeMappingAsync(tenant, program, secondControl, edition, secondCriterion, shared);
        var directory = new FitzControlMappingWorkItemDirectory(source.Kv);
        var locator = new ControlMappingReader(source.Reader, source.Events);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await locator.LocateAsync(tenant, program, shared));

        // Act
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzControlMappingWorkItemDirectory.ProjectorName,
            tenant, ControlCriterionMapping.Area, await source.FactsAsync(tenant, ControlCriterionMapping.Area));
        var pending = await directory.LoadProgramAsync(tenant, program);
        var review = await source.ReviewMappingAsync(first, shared);
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzControlMappingWorkItemDirectory.ProjectorName,
            tenant, ControlCriterionMapping.Area, [review]);
        var remaining = await directory.LoadProgramAsync(tenant, program);

        // Assert
        Assert.True(pending.IsSuccess, pending.Error?.Message);
        Assert.Equal(2, pending.Value.Count);
        Assert.Equal(2, pending.Value.Select(candidate => candidate.WorkItemId).Distinct().Count());
        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        var item = Assert.Single(remaining.Value);
        Assert.Equal(secondControl, item.ControlId);
        Assert.Equal($"Review control mapping for {secondCriterion}", item.Summary);
        Assert.Contains(pending.Value, original => original.WorkItemId == item.WorkItemId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothApplicabilityReviewsAndRemoveOnlyReviewedLineGivenReusedLocalDecisionId(bool sameCriterion)
    {
        // Arrange
        await using var source = new NativeSources();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var firstEdition = Uuid.CreateVersion4();
        var secondEdition = sameCriterion ? Uuid.CreateVersion4() : firstEdition;
        var secondCriterion = sameCriterion ? "CC6.1" : "CC7.1";
        var shared = Uuid.CreateVersion4();
        var first = await source.ProposeApplicabilityAsync(tenant, program, firstEdition, "CC6.1", shared);
        await source.ProposeApplicabilityAsync(tenant, program, secondEdition, secondCriterion, shared);
        var directory = new FitzCriterionApplicabilityWorkItemDirectory(source.Kv);
        var locator = new ApplicabilityReader(source.Reader, source.Events);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await locator.LocateAsync(tenant, program, shared));

        // Act
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            tenant, CriterionApplicability.Area, await source.FactsAsync(tenant, CriterionApplicability.Area));
        var pending = await directory.LoadProgramAsync(tenant, program);
        var review = await source.ReviewApplicabilityAsync(first, shared);
        await NativeSources.ProjectAsync(directory, directory.ApplyAsync, FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            tenant, CriterionApplicability.Area, [review]);
        var remaining = await directory.LoadProgramAsync(tenant, program);

        // Assert
        Assert.True(pending.IsSuccess, pending.Error?.Message);
        Assert.Equal(2, pending.Value.Count);
        Assert.Equal(2, pending.Value.Select(candidate => candidate.WorkItemId).Distinct().Count());
        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        var item = Assert.Single(remaining.Value);
        Assert.Equal($"Review not-applicable proposal for {secondCriterion}", item.Summary);
        Assert.Contains(pending.Value, original => original.WorkItemId == item.WorkItemId);
    }

    [Theory]
    [InlineData("commitment", "kv://bdgrz/commitment-draft-directory-v2/projection", "CommitmentDraftDirectoryV2", "commitment-drafts")]
    [InlineData("plan", "kv://bdgrz/accountable-work-items/control-operating-plans-v1", "AccountableWorkItemControlOperatingPlanV1", "control-operating-plans")]
    [InlineData("mapping", "kv://bdgrz/accountable-work-items/control-criterion-mappings-v1", "AccountableWorkItemControlCriterionMappingV1", "control-criterion-mappings")]
    [InlineData("applicability", "kv://bdgrz/accountable-work-items/criterion-applicability-v1", "AccountableWorkItemCriterionApplicabilityV1", "criterion-applicability")]
    public async Task ShouldRequireFreshReplayGivenCheckpointOfUnscopedProjectionGeneration(string family, string route,
        string oldName, string area)
    {
        // Arrange
        await using var source = new NativeSources();
        var tenant = Uuid.CreateVersion4();
        var legacy = new LegacyProjection(source.Kv, route, oldName);
        var identity = new CheckpointIdentity(oldName, EventStreamPattern.ForPattern(tenant.ToString(), area));
        var oldCheckpoint = new ProjectionCheckpoint(new EventCursor("old-unscoped-rows"));
        await using (var batch = await legacy.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await batch.CommitAsync(oldCheckpoint);

        // Act
        var actual = family switch
        {
            "commitment" => await new FitzCommitmentDraftDirectory(source.Kv,
                new CommitmentReader(source.Reader, source.Events, source.Content)).LoadCheckpointAsync(tenant),
            "plan" => await new FitzControlOperatingPlanWorkItemDirectory(source.Kv,
                new ControlReader(source.Reader, source.Events, source.Content), source.Content).LoadCheckpointAsync(tenant),
            "mapping" => await new FitzControlMappingWorkItemDirectory(source.Kv).LoadCheckpointAsync(tenant),
            _ => await new FitzCriterionApplicabilityWorkItemDirectory(source.Kv).LoadCheckpointAsync(tenant),
        };

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, actual);
        Assert.Equal(oldCheckpoint, await legacy.LoadCheckpointAsync(identity));
    }

    sealed class LegacyProjection(IKvClient client, string route, string name) : FitzKvProjectionStore(client, route, name) { }

    sealed class PlanRows(IKvClient client)
        : FitzKvProjectionStore(client, "kv://bdgrz/accountable-work-items/control-operating-plans-v2",
            FitzControlOperatingPlanWorkItemDirectory.ProjectorName)
    {
        public async Task<IReadOnlyList<ControlOperatingPlanApprovalWorkState>> ReadAsync(Uuid tenant, Uuid program)
        {
            await using var tx = await BeginReadAsync(tenant.ToString());
            return (await ControlOperatingPlanWorkItemDirectorySchema.Plans.QueryAsync(tx,
                ControlOperatingPlanWorkItemDirectorySchema.ByProgram.Query().WithPrefix(program.ToString()).Take(10))).Items;
        }
    }

    sealed class NativeSources : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public InMemoryKvClient Kv { get; } = new();
        public InMemoryEventStore Events { get; } = new();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IImmutableContentStore Content { get; }

        public NativeSources()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Content = new FitzKvImmutableContentStore(Kv);
        }

        public async Task<ControlOperatingPlanView> ProposePlanAsync(Uuid tenant, Uuid program, Uuid control, Uuid id)
        {
            var owner = new ControlOperatingPlan(tenant, program, control, id);
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Proposer");
            var plan = new ControlOperatingPlanView(tenant, program, control, id, 1, "pending_approval", Uuid.CreateVersion4(),
                new OperatingHolder("member", Uuid.CreateVersion4()), null, Uuid.CreateVersion4(),
                new ControlCadence("recurring", "monthly", new DateOnly(2026, 10, 1), 5), "Monthly", ["Review log"],
                new DateOnly(2026, 10, 1), null, "Run monthly review", member, actor, Now);
            var body = await Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), ControlOperatingContent.PlanSchema, plan);
            var (first, result) = await ResponsesAsync(tenant, ControlOperatingContent.PlanSchema, plan);
            Assert.True(owner.Propose(Operation(owner, id, "ProposeControlOperatingPlan", actor, 0), body, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return plan;
        }

        public async Task<DomainEvent> ApprovePlanAsync(ControlOperatingPlanView plan)
        {
            var owner = await Reader.HydrateAsync(new ControlOperatingPlan(plan.TenantId, plan.ProgramId, plan.ControlId));
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Approver");
            var approved = plan with
            {
                Revision = 2,
                Status = "approved",
                ApprovedBy = actor,
                ApprovedAt = Now.AddMinutes(1),
                ApprovalRationale = "Independent approval",
                Reassignments = [],
            };
            var (first, result) = await ResponsesAsync(plan.TenantId, ControlOperatingContent.ApprovedPlanSchema, approved);
            var moves = await Content.WriteJsonAsync(plan.TenantId.ToString(), Uuid.CreateVersion4(),
                ControlOperatingContent.ReassignmentsSchema, (IReadOnlyList<OccurrenceReassignmentView>)[]);
            Assert.True(owner.Approve(Operation(owner, Uuid.CreateVersion4(), "ApproveControlOperatingPlan", actor, 1),
                plan.PlanVersionId, member, actor, Now.AddMinutes(1), "Independent approval", null, moves, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return (await FactsAsync(plan.TenantId, ControlOperatingPlan.Area)).OfType<ControlOperatingPlanApproved>().Single();
        }

        public async Task<ControlCriterionMapping> ProposeMappingAsync(Uuid tenant, Uuid program, Uuid control,
            Uuid edition, string criterion, Uuid id)
        {
            var owner = new ControlCriterionMapping(tenant, program, control, edition, criterion, id);
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Proposer");
            var (first, result) = await ResponsesAsync(tenant, ControlCriterionMapping.RegistrationSchema,
                new ControlCriterionMappingRegistration(id, 1, 1));
            Assert.True(owner.Propose(Operation(owner, id, "ProposeControlCriterionMapping", actor, 0), Uuid.CreateVersion4(),
                "security", "Addresses criterion", "Applies to the environment", member, "Proposer", Now, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return owner;
        }

        public async Task<DomainEvent> ReviewMappingAsync(ControlCriterionMapping owner, Uuid id)
        {
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Reviewer");
            var (first, result) = await ResponsesAsync(Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(owner.Review(Operation(owner, Uuid.CreateVersion4(), "ReviewControlCriterionMapping", actor, 1),
                id, "accept", "Independent review", member, "Reviewer", Now.AddMinutes(1), null, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return (await FactsAsync(Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture), ControlCriterionMapping.Area)).OfType<ControlCriterionMappingReviewed>().Single();
        }

        public async Task<CriterionApplicability> ProposeApplicabilityAsync(Uuid tenant, Uuid program,
            Uuid edition, string criterion, Uuid id)
        {
            var owner = new CriterionApplicability(tenant, program, edition, criterion, id);
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Proposer");
            var (first, result) = await ResponsesAsync(tenant, CriterionApplicability.RegistrationSchema,
                new CriterionApplicabilityRegistration(id, 1, 1));
            Assert.True(owner.Propose(Operation(owner, id, "ProposeCriterionNotApplicable", actor, 0), "Outside scope",
                member, actor, Now, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return owner;
        }

        public async Task<DomainEvent> ReviewApplicabilityAsync(CriterionApplicability owner, Uuid id)
        {
            var member = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Reviewer");
            var (first, result) = await ResponsesAsync(Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(owner.Review(Operation(owner, Uuid.CreateVersion4(), "ReviewCriterionApplicability", actor, 1),
                id, "accept", "Independent review", member, actor, Now.AddMinutes(1), null, first, result).IsSuccess);
            await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
            return (await FactsAsync(Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture), CriterionApplicability.Area)).OfType<CriterionApplicabilityReviewed>().Single();
        }

        public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid tenant, string area)
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), area),
                EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    facts.Add(record.Event);
            return facts;
        }

        public static async Task ProjectAsync(IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply,
            string name, Uuid tenant, string area, IReadOnlyList<DomainEvent> facts)
        {
            var identity = new CheckpointIdentity(name, EventStreamPattern.ForPattern(tenant.ToString(), area));
            var current = name == FitzControlOperatingPlanWorkItemDirectory.ProjectorName
                ? await ((FitzControlOperatingPlanWorkItemDirectory)store).LoadCheckpointAsync(tenant)
                : name == FitzControlMappingWorkItemDirectory.ProjectorName
                    ? await ((FitzControlMappingWorkItemDirectory)store).LoadCheckpointAsync(tenant)
                    : await ((FitzCriterionApplicabilityWorkItemDirectory)store).LoadCheckpointAsync(tenant);
            await using var batch = await store.BeginAsync(new ProjectionBatchContext(identity, current));
            foreach (var fact in facts)
                await apply(fact, CancellationToken.None);
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor(Uuid.CreateVersion4().ToString())));
        }

        Task<(ImmutableContentRef First, ImmutableContentRef Result)> ResponsesAsync(Uuid tenant) =>
            ResponsesAsync(tenant, UnitResult.Schema, new UnitResult());

        async Task<(ImmutableContentRef First, ImmutableContentRef Result)> ResponsesAsync<T>(Uuid tenant, string schema, T value)
            where T : notnull
        {
            var body = await Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), schema, value);
            var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
                schema, new StandardResponseBody(body));
            var first = await FirstResponses.FreezeAsync(Content, tenant.ToString(), response);
            var result = await Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
                new MutationResultContent(schema, response.Body));
            return (first, result);
        }

        static OperationProvenance Operation(Aggregate owner, Uuid id, string kind, ActorReference actor, ulong revision) =>
            OwnerProvenance.For(owner, id, kind, actor, "initial", revision, new string('a', 64),
                revision == 0 ? Now : Now.AddMinutes(1));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
