using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
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
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Handlers;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentNativeContentTests
{
    [Fact]
    public async Task ShouldFreezeOriginalEmailReferenceGivenExactRetryAfterDisplayRename()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var authored = scenario.Request with
        {
            Kind = " SERVICE_COMMITMENT ",
            Identifier = " native ",
            Statement = " Statement 1 ",
            Context = " Context ",
            SourceReference = " MSA "
        };
        var first = await new CreateCommitmentDraftHandler(scenario.Source.Operations(), new ProviderSource(scenario.Source.Reader, scenario.Source.Content))
            .HandleAsync(CommitmentSourceFixture.Context(authored, scenario.Author, display: "original@example.test"), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await scenario.OwnerAsync();

        // Act
        var replay = await scenario.CreateDraftAsync("renamed@example.test");
        var fact = Assert.IsType<CommitmentDraftCreated>(Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id)));
        var after = await scenario.OwnerAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal("original@example.test", fact.Operation.Actor.Display);
        Assert.Equal("original@example.test", fact.ActorDisplay);
        Assert.Equal(RbacIds.Member(scenario.Tenant, scenario.Author).ToString(), fact.Operation.Actor.Id);
        Assert.NotEqual(fact.Operation.Actor.Id, fact.Operation.Actor.Display);
    }

    [Fact]
    public async Task ShouldRetainVerifiedHistoryOutsideCompactOwnerGivenManyCommitmentRevisions()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var handler = new ReviseCommitmentDraftHandler(scenario.Source.Operations());

        // Act
        for (var revision = 2; revision <= 70; revision++)
        {
            scenario.Source.Clock.Now = scenario.Source.Clock.Now.AddMinutes(1);
            var result = await handler.HandleAsync(CommitmentSourceFixture.Context(new ReviseCommitmentDraft(Uuid.CreateVersion4(),
                scenario.Tenant, scenario.Program, scenario.Id, revision - 1, $"Statement {revision}", "Context", "MSA"), scenario.Author), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        var owner = await scenario.OwnerAsync();
        var facts = await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id);
        var original = await CommitmentFactContent.ReadChangeAsync(scenario.Source.Content, Assert.IsType<CommitmentDraftCreated>(facts[0]));
        var current = await scenario.Source.Commitments.ReadAsync(scenario.Tenant, scenario.Program, scenario.Id);

        // Assert
        Assert.Equal(70, owner.NativeSequence);
        Assert.Equal(70, owner.Revision);
        Assert.Equal("Statement 1", Assert.IsType<CommitmentDraftCreatedContent>(Assert.Single(original.Changes)).Statement);
        Assert.Equal("Statement 70", current.Draft!.Statement);
        Assert.Equal(70, current.ReadRevisions().Count);
        Assert.All(facts, fact => Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!).Length < 12 * 1024));
        foreach (var fact in facts)
        {
            var native = Assert.IsAssignableFrom<ICommitmentFact>(fact);
            var wrapper = await scenario.Source.Content.ReadJsonAsync<MutationResultContent>(scenario.Tenant.ToString(), native.OperationResult!);
            var descriptor = await FirstResponses.ReplayAsync(scenario.Source.Content, scenario.Tenant.ToString(), native.FirstResponse!);
            Assert.Equal(descriptor.ResultSchema, wrapper.Schema);
            Assert.Equal(descriptor.Body, wrapper.Value);
            Assert.Equal(200, descriptor.HttpStatus);
        }
        Assert.DoesNotContain("Statement 1", JsonSerializer.Serialize(owner.Header,
            ComplianceCoreJsonContext.Default.CommitmentAuthorityHeader), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldResolveOriginalReceiptBeforePreparationGivenUnreadableCommitmentHistory(bool unavailable)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var request = new ReviseCommitmentDraft(Uuid.CreateVersion4(), scenario.Tenant, scenario.Program, scenario.Id, 1,
            "Statement 2", "Context", "MSA");
        var writer = new ReviseCommitmentDraftHandler(scenario.Source.Operations());
        Assert.True((await writer.HandleAsync(CommitmentSourceFixture.Context(request, scenario.Author), CancellationToken.None)).IsSuccess);
        Assert.True((await writer.HandleAsync(CommitmentSourceFixture.Context(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
            Statement = "Statement 3"
        }, scenario.Author), CancellationToken.None)).IsSuccess);
        var before = await scenario.OwnerAsync();
        var missing = new UnreadableSchema(scenario.Source.Content, CommitmentFactContent.ChangeSchema, unavailable);
        var handler = new ReviseCommitmentDraftHandler(scenario.Source.Operations(missing));

        // Act
        var replay = await handler.HandleAsync(CommitmentSourceFixture.Context(request, scenario.Author), CancellationToken.None);
        var changedActor = await handler.HandleAsync(CommitmentSourceFixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var changedIntent = await handler.HandleAsync(CommitmentSourceFixture.Context(request with { Statement = "Changed intent" }, scenario.Author), CancellationToken.None);
        var changedGate = await handler.HandleAsync(CommitmentSourceFixture.Context(request with { ExpectedRevision = 3 }, scenario.Author), CancellationToken.None);
        Assert.Equal(0, missing.RejectedReads);
        var fresh = await handler.HandleAsync(CommitmentSourceFixture.Context(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 3,
            Statement = "Statement 4"
        }, scenario.Author), CancellationToken.None);
        var after = await scenario.OwnerAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedGate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fresh.Error?.Kind);
        Assert.Equal(unavailable, fresh.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(1, missing.RejectedReads);
    }

    [Fact]
    public async Task ShouldFreezeExactWaiverAndReplayOriginalReviewGivenLaterExpiry()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var member = RbacIds.Member(scenario.Tenant, scenario.Author);
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Commitment, scenario.Id, scenario.Id, 1, SeparationOfDutiesActions.Review);
        var waiver = new SeparationOfDutiesWaiver(scenario.Tenant, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, member, Uuid.CreateVersion4(), "Requester", "Exact independent exception",
            scenario.Source.Clock.Now.AddMinutes(-2), scenario.Source.Clock.Now.AddHours(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Independent approver", scenario.Source.Clock.Now.AddMinutes(-1)));
        await scenario.Source.Writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var request = new ReviewCommitmentDraft(Uuid.CreateVersion4(), scenario.Tenant, scenario.Program, scenario.Id, 1, "accept", "Verified",
            "Security owner", "applicable", "supported", SourceVerifiedReference: "MSA", SourceEvidence: "Signed MSA section checked",
            SeparationOfDutiesWaiverId: waiver.Id);
        var handler = new ReviewCommitmentDraftHandler(scenario.Source.Operations());
        Assert.True((await handler.HandleAsync(CommitmentSourceFixture.Context(request, scenario.Author, http: true), CancellationToken.None)).IsSuccess);
        var before = await scenario.OwnerAsync();
        scenario.Source.Clock.Now = scenario.Source.Clock.Now.AddDays(1);

        // Act
        var replay = await handler.HandleAsync(CommitmentSourceFixture.Context(request, scenario.Author, http: true), CancellationToken.None);
        var fresh = await handler.HandleAsync(CommitmentSourceFixture.Context(request with { CommandId = Uuid.CreateVersion4() },
            scenario.Author, http: true), CancellationToken.None);
        var fact = Assert.IsType<CommitmentReviewed>((await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id))[^1]);
        var mutation = await CommitmentFactContent.ReadChangeAsync(scenario.Source.Content, fact);
        var proof = Assert.Single(mutation.Waivers!);
        var after = await scenario.OwnerAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, fresh.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(waiver.Id, proof.WaiverId);
        Assert.Equal(member, proof.ActorId);
        Assert.Equal(member, proof.SubjectId);
        Assert.Equal(scope.Action, proof.Action);
        Assert.Equal(scope.RecordType, proof.TargetType);
        Assert.Equal(scope.RecordId, proof.TargetId);
        Assert.Equal(scope.VersionId, proof.TargetVersionId);
        Assert.Equal(scope.Revision, proof.TargetRevision);
        Assert.Equal(waiver.Stream, proof.ApprovalFact.Source);
        Assert.Equal(1UL, proof.ApprovalFact.ResourcePosition);
        Assert.Equal(2UL, proof.ApprovalFact.AggregateVersion);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("gate")]
    [InlineData("authority")]
    [InlineData("verified_wrong_authority")]
    public async Task ShouldRejectMalformedNativeAcceptanceGivenExactCommitmentSourceCut(string defect)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.Source.Commitments.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var before = rules.Authority(owner.Header!);
        var member = RbacIds.Member(scenario.Tenant, Uuid.CreateVersion4());
        var command = Uuid.CreateVersion4();
        Assert.Null(rules.Review(scenario.Program, 1, command, "accept", "Owner", "applicable", "supported", null,
            "Reviewed", member, "Reviewer", scenario.Source.Clock.Now, sourceVerifiedReference: "MSA", sourceEvidence: "Signed MSA checked"));
        var mutation = new CommitmentMutationContent(rules.PendingChanges.ToArray());
        var operation = OwnerProvenance.For(owner, command, nameof(ReviewCommitmentDraft), ActorReference.ForMember(member, "Reviewer"),
            scenario.Id.ToString(), 1, AuthoredIntentDigest.Compute(new ReviewCommitmentDraft(command, scenario.Tenant, scenario.Program,
                scenario.Id, 1, "accept", "Reviewed", "Owner", "applicable", "supported", SourceVerifiedReference: "MSA", SourceEvidence: "Signed MSA checked")),
            scenario.Source.Clock.Now);
        if (defect == "gate")
            operation = operation with { OriginalExpectedRevision = 9 };
        var fact = Assert.IsType<CommitmentReviewed>(await CommitmentSeeds.FreezeAsync(scenario.Source.Content, rules, mutation, operation, 2));
        var after = rules.Authority(fact.Header);
        if (defect == "actor")
            fact = fact with { ActorMemberId = RbacIds.Member(scenario.Tenant, scenario.Author) };
        if (defect == "authority")
            fact = fact with { Authority = owner.CurrentAuthority! };
        if (defect == "verified_wrong_authority")
        {
            after = after with { PendingAuthorMemberIds = [] };
            fact = fact with
            {
                Authority = await scenario.Source.Content.WriteJsonAsync(scenario.Tenant.ToString(), Uuid.CreateVersion4(),
                CommitmentFactContent.AuthoritySchema, after)
            };
        }

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, mutation, before, after);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id));
    }

    sealed class Scenario : IAsyncDisposable
    {
        public CommitmentSourceFixture Source { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Service { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Id => CommitmentDraft.IdFor(Tenant, Program, "service_commitment", "NATIVE");
        public CreateCommitmentDraft Request => new(Id, Tenant, Program, Service, "service_commitment", "NATIVE", "Statement 1", "Context", "MSA");
        public static async Task<Scenario> CreateAsync()
        {
            var scenario = new Scenario();
            var service = new ProductClientServiceRules(scenario.Tenant, scenario.Service);
            Assert.True(service.Create(scenario.Program, "Service", "Purpose", "Owner", scenario.Author,
                "Author", scenario.Source.Clock.Now).IsSuccess);
            await ProductClientServiceSeeds.WriteAsync(scenario.Source.Reader, scenario.Source.Writer, scenario.Source.Events, scenario.Source.Content, service);
            return scenario;
        }
        public ValueTask<Result<CommitmentDraftRegistration>> CreateDraftAsync(string display = "author@example.test") =>
            new CreateCommitmentDraftHandler(Source.Operations(), new ProviderSource(Source.Reader, Source.Content))
                .HandleAsync(CommitmentSourceFixture.Context(Request, Author, display: display), CancellationToken.None);
        public async Task<CommitmentDraft> OwnerAsync() => await Source.Reader.HydrateAsync(new CommitmentDraft(Tenant, Id));
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }
    sealed class UnreadableSchema(IImmutableContentStore inner, string schema, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string contentSchema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, contentSchema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("Commitment content is temporarily absent.")
                : new ImmutableContentIntegrityException("Commitment content digest differs.");
        }
    }
}
