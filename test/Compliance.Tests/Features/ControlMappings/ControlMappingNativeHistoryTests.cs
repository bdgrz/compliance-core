using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class ControlMappingNativeHistoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldRejectUnselectedHistoricalProposalReuseGivenDirectOrExecutorHydration(bool applicability,
        bool execute)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var seeded = await fixture.SeedAsync(applicability, "reject");
        var first = (IMappingLineFact)seeded.Facts[0];
        var repeatedHeader = first.Header with
        {
            Revision = 3,
            VersionCount = 2,
            Pending = first.Header.Pending! with { VersionNumber = 2 }
        };
        var provenance = first.Operation with { OriginalExpectedRevision = 2 };
        DomainEvent repeated = seeded.Facts[0] switch
        {
            ControlCriterionMappingProposed mapping => mapping with
            { Revision = 3, VersionNumber = 2, Header = repeatedHeader, Operation = provenance },
            CriterionNotApplicableProposed decision => decision with
            { Revision = 3, VersionNumber = 2, Header = repeatedHeader, Operation = provenance },
            _ => throw new InvalidOperationException(),
        };
        DomainEventSeed.Attach(repeated, seeded.Owner.Id, 3);
        await fixture.Events.AppendAsync(seeded.Owner.Stream, 2, [repeated]);
        var entered = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (applicability)
            {
                var owner = (CriterionApplicability)MappingLineContent.Owner(repeated);
                if (execute)
                    await fixture.Executor.ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await fixture.Reader.HydrateAsync(owner);
            }
            else
            {
                var owner = (ControlCriterionMapping)MappingLineContent.Owner(repeated);
                if (execute)
                    await fixture.Executor.ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await fixture.Reader.HydrateAsync(owner);
            }
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(entered);
        Assert.Equal(3, await NativeFacts.CountAsync(fixture.Services, fixture.Tenant, seeded.Owner.Stream.Area));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRollbackRowsAndCheckpointGivenVerifiedBodyWithSubstitutedBusinessChange(bool applicability)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var seeded = await fixture.SeedAsync(applicability, "accept");
        var review = (IMappingLineFact)seeded.Facts[1];
        var body = await NativeContent.ReadAsync<MappingLineMutation>(fixture.Content, fixture.Tenant, review.Content);
        var decoded = await MappingLineContent.ReadAsync(fixture.Content, seeded.Facts[1], CancellationToken.None);
        object wrong = decoded.Change switch
        {
            ControlCriterionMappingReviewedChange value => value with { Rationale = "Another review rationale." },
            CriterionApplicabilityReviewedChange value => value with { Rationale = "Another review rationale." },
            _ => throw new InvalidOperationException(),
        };
        var altered = body with { Change = JsonSerializer.SerializeToElement(wrong, wrong.GetType(), ComplianceCoreJsonContext.Default) };
        var reference = await NativeContent.WriteAsync(fixture.Content, fixture.Tenant, MappingLineContent.Schema, altered);
        DomainEvent malformed = seeded.Facts[1] switch
        {
            ControlCriterionMappingReviewed mapping => mapping with { Content = reference },
            CriterionApplicabilityReviewed decision => decision with { Content = reference },
            _ => throw new InvalidOperationException(),
        };
        malformed.AttachMetadata(seeded.Facts[1].Metadata);
        var mappingDirectory = new FitzControlMappingDirectoryV1(new InMemoryKvClient(), fixture.Content);
        var applicabilityDirectory = new FitzCriterionApplicabilityDirectoryV1(new InMemoryKvClient(), fixture.Content);
        IProjectionStore directory = applicability ? applicabilityDirectory : mappingDirectory;
        Func<DomainEvent, CancellationToken, ValueTask> apply = applicability
            ? applicabilityDirectory.ApplyAsync : mappingDirectory.ApplyAsync;
        var name = applicability ? FitzCriterionApplicabilityDirectoryV1.ProjectorName : FitzControlMappingDirectoryV1.ProjectorName;
        var identity = new CheckpointIdentity(name, EventStreamPattern.ForPattern(fixture.Tenant.ToString(), seeded.Owner.Stream.Area));
        Exception? failure;

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await apply(seeded.Facts[0], CancellationToken.None);
            failure = await Record.ExceptionAsync(async () => await apply(malformed, CancellationToken.None));
        }
        var checkpoint = applicability ? await applicabilityDirectory.LoadCheckpointAsync(fixture.Tenant)
            : await mappingDirectory.LoadCheckpointAsync(fixture.Tenant);
        var rows = applicability ? (await applicabilityDirectory.ListProgramAsync(fixture.Tenant, fixture.Program, 10, null)).Items.Count
            : (await mappingDirectory.ListProgramAsync(fixture.Tenant, fixture.Program, 10, null)).Items.Count;

        // Assert
        Assert.NotEqual(review.Content, reference);
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(0, rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectCurrentAuthoritySubstitutionGivenQualifiedImmutableMutationReference(bool applicability)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var seeded = await fixture.SeedAsync(applicability, "accept");
        var original = (IMappingLineFact)seeded.Facts[1];
        var body = await NativeContent.ReadAsync<MappingLineMutation>(fixture.Content, fixture.Tenant, original.Content);
        var reference = await NativeContent.WriteAsync(fixture.Content, fixture.Tenant, MappingLineContent.Schema,
            body with { Current = MappingLineContent.Json(new UnitResult()) });
        var modified = seeded.Facts.Select(fact => fact switch
        {
            ControlCriterionMappingReviewed value => value with { Content = reference },
            CriterionApplicabilityReviewed value => value with { Content = reference },
            _ => fact,
        }).ToArray();
        for (var index = 0; index < modified.Length; index++)
            if (!ReferenceEquals(modified[index], seeded.Facts[index]))
                modified[index].AttachMetadata(seeded.Facts[index].Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(seeded.Owner.Stream, 0, modified);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (applicability)
                await new ApplicabilityReader(fixture.Reader, events, fixture.Content).ReadProgramAsync(fixture.Tenant, fixture.Program);
            else
                await new ControlMappingReader(fixture.Reader, events, fixture.Content).ReadProgramAsync(fixture.Tenant, fixture.Program);
        });

        // Assert
        Assert.NotEqual(original.Content, reference);
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalRegistrationWithoutCurrentMutationReadGivenAcceptedProposal(bool applicability)
    {
        // Arrange
        FaultingContent? content = null;
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync(services =>
        {
            var original = (IImmutableContentStore)services.Last(item => item.ServiceType == typeof(IImmutableContentStore)).ImplementationInstance!;
            content = new FaultingContent(original);
            services.AddSingleton<IImmutableContentStore>(content);
        });
        await using var provider = fixture.Provider;
        var mapping = fixture.Propose("CC6.1");
        var decision = new ProposeCriterionNotApplicable(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            mapping.EditionId, "CC6.2", 0, "Outside the selected environment.");
        object originalResponse;
        if (applicability)
        {
            originalResponse = (await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId, decision)).Value;
            await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.ReviewerUserId,
                fixture.ReviewApplicability(decision.DecisionId, 1, "reject"));
        }
        else
        {
            originalResponse = await fixture.ProposeAsync(mapping);
            await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.ReviewerUserId,
                fixture.Review(mapping.MappingId, 1, "reject"));
        }
        content!.Fail = true;

        // Act
        object replay;
        Result changed;
        if (applicability)
        {
            replay = (await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId, decision)).Value;
            var changedDecision = await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId,
                decision with { Rationale = "Changed intent." }, RequestErrorKind.Conflict);
            changed = Result.Failure(changedDecision.Error!);
        }
        else
        {
            replay = await fixture.ProposeAsync(mapping);
            var result = await fixture.Scenario(fixture.AuthorUserId).When(mapping with { Rationale = "Changed intent." })
                .ExpectFailure(RequestErrorKind.Conflict);
            changed = Result.Failure(result.Error!);
        }

        // Assert
        Assert.Equal(originalResponse, replay);
        Assert.False(changed.IsSuccess);
        Assert.Equal(0, content.BlockedReads);
        Assert.Equal(2, await NativeFacts.CountAsync(provider, fixture.TenantId,
            applicability ? CriterionApplicability.Area : ControlCriterionMapping.Area));
        if (applicability)
        {
            var unavailable = await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId,
                decision with { DecisionId = Uuid.CreateVersion4(), ExpectedRevision = 2 }, RequestErrorKind.Conflict);
            Assert.True(unavailable.Error!.IsTransient);
        }
        else
        {
            var unavailable = await fixture.Scenario(fixture.AuthorUserId).When(mapping with
            { MappingId = Uuid.CreateVersion4(), ExpectedRevision = 2 }).ExpectFailure(RequestErrorKind.Conflict);
            Assert.True(unavailable.Error!.IsTransient);
        }
        Assert.True(content.BlockedReads > 0);
        Assert.Equal(2, await NativeFacts.CountAsync(provider, fixture.TenantId,
            applicability ? CriterionApplicability.Area : ControlCriterionMapping.Area));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldComposeFirstIdentityAndTerminalStatusGivenLaterRejectedProposal(bool applicability)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var writer = fixture.Services.GetRequiredService<IAggregateWriter>();

        // Act
        if (applicability)
        {
            var rules = new CriterionApplicabilityRules(fixture.Tenant, fixture.Program, fixture.Edition, "CC6.1");
            Assert.True(rules.Propose(MappingRuleFacts.Root(rules, first, nameof(ProposeCriterionNotApplicable), 0, author),
                "Outside scope.", author, ActorReference.ForMember(author, "Author"), NativeFacts.Now).IsSuccess);
            Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewCriterionApplicability), 1, reviewer),
                first, "accept", "Independent acceptance.", reviewer, ActorReference.ForMember(reviewer, "Reviewer"), NativeFacts.Now, null).IsSuccess);
            Assert.True(rules.Withdraw(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(WithdrawCriterionNotApplicable), 2, author),
                first, "Now applicable.", ActorReference.ForMember(author, "Author"), NativeFacts.Now).IsSuccess);
            Assert.True(rules.Propose(MappingRuleFacts.Root(rules, second, nameof(ProposeCriterionNotApplicable), 3, author),
                "Another proposal.", author, ActorReference.ForMember(author, "Author"), NativeFacts.Now).IsSuccess);
            Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewCriterionApplicability), 4, reviewer),
                second, "reject", "Coverage required.", reviewer, ActorReference.ForMember(reviewer, "Reviewer"), NativeFacts.Now, null).IsSuccess);
            await MappingNativeSeeds.AppendAsync(fixture.Reader, writer, fixture.Content, rules);
            var reader = new ApplicabilityReader(fixture.Reader, fixture.Events, fixture.Content);
            var owner = await reader.ReadLineAsync(fixture.Tenant, fixture.Program, fixture.Edition, "CC6.1");
            var history = await reader.ComposeAsync(owner);
            var current = await NativeContent.ReadAsync<MappingLineMutation>(fixture.Content, fixture.Tenant, owner.CurrentContent!);
            var authority = current.Current.Deserialize(ComplianceCoreJsonContext.Default.CriterionApplicabilityView)!;

            // Assert
            Assert.Equal(first, history.View!.DecisionId);
            Assert.Equal("withdrawn", history.View.Status);
            Assert.Equal(["withdrawn", "rejected"], history.View.Versions.Select(static value => value.Status));
            Assert.Equal(5, owner.Revision);
            Assert.Equal(2, owner.Header.VersionCount);
            Assert.True(owner.Header.EverTerminated);
            Assert.Empty(authority.Versions);
            Assert.Null(owner.Header.Pending);
            Assert.Null(owner.Header.Active);
        }
        else
        {
            var rules = new ControlCriterionMappingRules(fixture.Tenant, fixture.Program, fixture.Control, fixture.Edition, "CC6.1");
            Assert.True(rules.Propose(MappingRuleFacts.Root(rules, first, nameof(ProposeControlCriterionMapping), 0, author),
                Uuid.CreateVersion4(), "criterion", "Addresses criterion.", "Scoped systems.", author, "Author", NativeFacts.Now).IsSuccess);
            Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewControlCriterionMapping), 1, reviewer),
                first, "accept", "Independent acceptance.", reviewer, "Reviewer", NativeFacts.Now, null).IsSuccess);
            Assert.True(rules.Retire(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(RetireControlCriterionMapping), 2, author),
                first, "Replaced.", author, "Author", NativeFacts.Now).IsSuccess);
            Assert.True(rules.Propose(MappingRuleFacts.Root(rules, second, nameof(ProposeControlCriterionMapping), 3, author),
                Uuid.CreateVersion4(), "criterion", "Another proposal.", "Scoped systems.", author, "Author", NativeFacts.Now).IsSuccess);
            Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewControlCriterionMapping), 4, reviewer),
                second, "reject", "Coverage required.", reviewer, "Reviewer", NativeFacts.Now, null).IsSuccess);
            await MappingNativeSeeds.AppendAsync(fixture.Reader, writer, fixture.Content, rules);
            var reader = new ControlMappingReader(fixture.Reader, fixture.Events, fixture.Content);
            var owner = await reader.ReadLineAsync(fixture.Tenant, fixture.Program, fixture.Control, fixture.Edition, "CC6.1");
            var history = await reader.ComposeAsync(owner);
            var current = await NativeContent.ReadAsync<MappingLineMutation>(fixture.Content, fixture.Tenant, owner.CurrentContent!);
            var authority = current.Current.Deserialize(ComplianceCoreJsonContext.Default.ControlCriterionMappingView)!;

            // Assert
            Assert.Equal(first, history.View!.MappingId);
            Assert.Equal("retired", history.View.Status);
            Assert.Equal(["retired", "rejected"], history.View.Versions.Select(static value => value.Status));
            Assert.Equal(5, owner.Revision);
            Assert.Equal(2, owner.Header.VersionCount);
            Assert.True(owner.Header.EverTerminated);
            Assert.Empty(authority.Versions);
            Assert.Null(owner.Header.Pending);
            Assert.Null(owner.Header.Active);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectAcceptedRetryGivenUnselectedDuplicateHistoryOnSameLine(bool applicability)
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        await using var provider = fixture.Provider;
        var mapping = fixture.Propose("CC6.1");
        var decision = new ProposeCriterionNotApplicable(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            mapping.EditionId, "CC6.2", 0, "Outside the environment.");
        if (applicability)
        {
            await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId, decision);
            await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.ReviewerUserId,
                fixture.ReviewApplicability(decision.DecisionId, 1, "reject"));
        }
        else
        {
            await fixture.ProposeAsync(mapping);
            await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.ReviewerUserId,
                fixture.Review(mapping.MappingId, 1, "reject"));
        }
        await using var scope = provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        DomainEvent? first = null;
        EventStreamAddress? stream = null;
        var area = applicability ? CriterionApplicability.Area : ControlCriterionMapping.Area;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(fixture.TenantId.ToString(), area),
                           EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is ControlCriterionMappingProposed or CriterionNotApplicableProposed)
            { first = record.Event; stream = record.Stream; break; }
        }
        var native = Assert.IsAssignableFrom<IMappingLineFact>(first);
        var header = native.Header with
        {
            Revision = 3,
            VersionCount = 2,
            Pending = native.Header.Pending! with { VersionNumber = 2 }
        };
        var operation = native.Operation with { OriginalExpectedRevision = 2 };
        DomainEvent repeated = first switch
        {
            ControlCriterionMappingProposed value => value with { Revision = 3, VersionNumber = 2, Header = header, Operation = operation },
            CriterionNotApplicableProposed value => value with { Revision = 3, VersionNumber = 2, Header = header, Operation = operation },
            _ => throw new InvalidOperationException(),
        };
        DomainEventSeed.Attach(repeated, first!.Metadata.AggregateId, 3);
        await scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(stream!, 2, [repeated]);

        // Act
        RequestError? error;
        if (applicability)
            error = (await PersonalMappingDecisionTransportTests.HttpAsync(provider, fixture.AuthorUserId,
                decision, RequestErrorKind.Conflict)).Error;
        else
            error = (await fixture.Scenario(fixture.AuthorUserId).When(mapping).ExpectFailure(RequestErrorKind.Conflict)).Error;

        // Assert
        Assert.NotNull(error);
        Assert.False(error.IsTransient);
        Assert.Equal(3, await NativeFacts.CountAsync(provider, fixture.TenantId, area));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectIntermediatePayloadSubstitutionGivenSameHydratedHeaderAndMetadataCut(bool applicability)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var seeded = await fixture.SeedAsync(applicability, "accept");
        var first = seeded.Facts[0];
        var otherContent = ((IMappingLineFact)seeded.Facts[1]).Content;
        DomainEvent substituted = first switch
        {
            ControlCriterionMappingProposed value => value with { Content = otherContent },
            CriterionNotApplicableProposed value => value with { Content = otherContent },
            _ => throw new InvalidOperationException(),
        };
        substituted.AttachMetadata(first.Metadata);
        var reader = new MappingLineAggregateReader(fixture.Reader,
            new Records(seeded.Owner.Stream, [substituted, seeded.Facts[1]]));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (applicability)
                await reader.HydrateAsync((CriterionApplicability)MappingLineContent.Owner(first));
            else
                await reader.HydrateAsync((ControlCriterionMapping)MappingLineContent.Owner(first));
        });

        // Assert
        Assert.Equal(first.Metadata, substituted.Metadata);
        Assert.Equal(((IMappingLineFact)first).Header, ((IMappingLineFact)substituted).Header);
        Assert.NotEqual(((IMappingLineFact)first).Content, ((IMappingLineFact)substituted).Content);
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainUnavailableClassificationGivenTruncatedExactHydratedCut(bool applicability)
    {
        // Arrange
        await using var fixture = new NativeFixture();
        var seeded = await fixture.SeedAsync(applicability, "accept");
        var reader = new MappingLineAggregateReader(fixture.Reader, new Records(seeded.Owner.Stream, [seeded.Facts[0]]));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (applicability)
                await reader.HydrateAsync((CriterionApplicability)MappingLineContent.Owner(seeded.Facts[0]));
            else
                await reader.HydrateAsync((ControlCriterionMapping)MappingLineContent.Owner(seeded.Facts[0]));
        });

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
        Assert.Equal(2, await NativeFacts.CountAsync(fixture.Services, fixture.Tenant, seeded.Owner.Stream.Area));
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }

    sealed class FaultingContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool Fail { get; set; }
        public int BlockedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (Fail && reference.Schema == MappingLineContent.Schema)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("The selected immutable mutation is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class NativeFixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Control { get; } = Uuid.CreateVersion4();
        public Uuid Edition { get; } = Uuid.CreateVersion4();
        public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());
        public InMemoryEventStore Events { get; } = new();
        public IServiceProvider Services => _scope.ServiceProvider;
        public IAggregateReader Reader => Services.GetRequiredService<IAggregateReader>();
        public IAggregateExecutor Executor => Services.GetRequiredService<IAggregateExecutor>();
        public NativeFixture()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddPortia();
            var original = services.Last(value => !value.IsKeyedService && value.ServiceType == typeof(IAggregateReader));
            var key = new object();
            services.Remove(original);
            if (original.ImplementationFactory is { } factory)
                services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key, (provider, _) => factory(provider), original.Lifetime));
            else
                services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key, original.ImplementationType!, original.Lifetime));
            services.Add(ServiceDescriptor.Describe(typeof(IAggregateReader), provider =>
                new MappingLineAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(key), Events), original.Lifetime));
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
        }
        public async Task<(Aggregate Owner, DomainEvent[] Facts)> SeedAsync(bool applicability, string outcome)
        {
            var author = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            var id = Uuid.CreateVersion4();
            Aggregate owner;
            if (applicability)
            {
                var rules = new CriterionApplicabilityRules(Tenant, Program, Edition, "CC6.1");
                Assert.True(rules.Propose(MappingRuleFacts.Root(rules, id, nameof(ProposeCriterionNotApplicable), 0, author),
                    "Outside the environment.", author, ActorReference.ForMember(author, "Author"), NativeFacts.Now).IsSuccess);
                Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewCriterionApplicability), 1, reviewer),
                    id, outcome, "Independent review.", reviewer, ActorReference.ForMember(reviewer, "Reviewer"), NativeFacts.Now, null).IsSuccess);
                owner = await MappingNativeSeeds.BuildAsync(rules, Content);
            }
            else
            {
                var rules = new ControlCriterionMappingRules(Tenant, Program, Control, Edition, "CC6.1");
                Assert.True(rules.Propose(MappingRuleFacts.Root(rules, id, nameof(ProposeControlCriterionMapping), 0, author),
                    Uuid.CreateVersion4(), "criterion", "Addresses criterion.", "All scoped systems.", author, "Author", NativeFacts.Now).IsSuccess);
                Assert.True(rules.Review(MappingRuleFacts.Root(rules, Uuid.CreateVersion4(), nameof(ReviewControlCriterionMapping), 1, reviewer),
                    id, outcome, "Independent review.", reviewer, "Reviewer", NativeFacts.Now, null).IsSuccess);
                owner = await MappingNativeSeeds.BuildAsync(rules, Content);
            }
            var facts = owner is ControlCriterionMapping mapping ? NativeFacts.Raised(mapping) : NativeFacts.Raised((CriterionApplicability)owner);
            await Events.AppendAsync(owner.Stream, 0, facts);
            return (owner, facts);
        }
        public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
    }
}
