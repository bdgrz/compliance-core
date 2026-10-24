using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantLifecycleIdentityTests
{
    [Theory]
    [InlineData("suspend", "actor")]
    [InlineData("suspend", "reason")]
    [InlineData("reactivate", "actor")]
    [InlineData("reactivate", "reason")]
    [InlineData("offboard", "actor")]
    [InlineData("offboard", "reason")]
    public async Task ShouldRefuseChangedOriginalBindingGivenAcceptedLifecycleCommand(string kind, string change)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(kind == "reactivate");
        var id = Uuid.CreateVersion4();
        var revision = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(kind, id, "Original reason", fixture.Operator, revision)).IsSuccess);
        var before = await fixture.CurrentAsync();

        // Act
        var result = await fixture.SendAsync(kind, id, change == "reason" ? "Changed reason" : "Original reason",
            change == "actor" ? Uuid.CreateVersion4() : fixture.Operator, revision);
        var after = await fixture.CurrentAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task ShouldReplayOriginalWithoutRestoringOldStateGivenLaterLifecycleCommand(string kind)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(kind == "reactivate");
        var id = Uuid.CreateVersion4();
        var revision = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(kind, id, "Original reason", fixture.Operator, revision)).IsSuccess);
        var inverse = kind == "suspend" ? "reactivate" : "suspend";
        var nextRevision = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(inverse, Uuid.CreateVersion4(), "Later reason", fixture.Operator,
            nextRevision)).IsSuccess);
        var before = await fixture.CurrentAsync();

        // Act
        var replay = await fixture.SendAsync(kind, id, "Original reason", fixture.Operator, revision);
        var after = await fixture.CurrentAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(before.IsSuspended, after.IsSuspended);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("offboard")]
    public async Task ShouldRefuseChangedOriginalRevisionGivenAcceptedLifecycleCommand(string kind)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(false);
        var id = Uuid.CreateVersion4();
        var revision = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(kind, id, "Original", fixture.Operator, revision)).IsSuccess);
        var before = await fixture.CurrentAsync();

        // Act
        var changed = await fixture.SendAsync(kind, id, "Original", fixture.Operator, revision + 1);
        var after = await fixture.CurrentAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task ShouldRetainOriginalNoChangeDecisionGivenLaterOppositeLifecycleChange(string kind)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(kind == "suspend");
        var id = Uuid.CreateVersion4();
        var revision = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(kind, id, "Confirm original state", fixture.Operator, revision)).IsSuccess);
        var inverse = kind == "suspend" ? "reactivate" : "suspend";
        var next = (long)(await fixture.CurrentAsync()).Revision;
        Assert.True((await fixture.SendAsync(inverse, Uuid.CreateVersion4(), "Change later", fixture.Operator, next)).IsSuccess);
        var before = await fixture.CurrentAsync();

        // Act
        var replay = await fixture.SendAsync(kind, id, "Confirm original state", fixture.Operator, revision);
        var after = await fixture.CurrentAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(before.IsSuspended, after.IsSuspended);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPersistOriginalResultEnvelopesAndProjectTerminalStatusGivenNativeLifecycle()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(false);
        foreach (var kind in new[] { "suspend", "reactivate", "offboard" })
            Assert.True((await fixture.SendAsync(kind, Uuid.CreateVersion4(), "Reason", fixture.Operator,
                (long)(await fixture.CurrentAsync()).Revision)).IsSuccess);
        var (facts, cursor) = await fixture.FactsAsync();
        var directory = fixture.Directory();

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            "TenantDirectory", EventStreamPattern.ForPattern("bdgrz", "tenants")), ProjectionCheckpoint.Start)))
        {
            foreach (var fact in facts)
                await directory.ApplyAsync(fact);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var projected = await directory.GetAsync(fixture.TenantId);
        var replay = new AggregateScenario<Tenant>(new(fixture.TenantId)).Given(facts).Aggregate;

        // Assert
        Assert.True(replay.IsOffboarding);
        Assert.Equal("offboarding", projected!.Status);
        var results = facts.Select(fact => fact switch
        {
            TenantSuspendedV2 suspended => suspended.OperationResult,
            TenantReactivatedV2 reactivated => reactivated.OperationResult,
            TenantOffboardingStartedV2 offboarded => offboarded.OperationResult,
            _ => null,
        }).Where(reference => reference is not null).ToArray();
        Assert.Equal(3, results.Length);
        foreach (var reference in results)
        {
            Assert.Equal("mutation-result-content.v1", reference!.Schema);
            var result = await fixture.Content.ReadJsonAsync<MutationResultContent>("bdgrz", reference);
            Assert.Equal(UnitResult.Schema, result.Schema);
            var body = Assert.IsType<StandardResponseBody>(result.Value);
            _ = await fixture.Content.ReadJsonAsync<UnitResult>("bdgrz", body.Content);
        }
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("revision")]
    public async Task ShouldRejectMalformedNativeFactGivenForeignLifecycleBinding(string variant)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(false);
        Assert.True((await fixture.SendAsync("suspend", Uuid.CreateVersion4(), "Reason", fixture.Operator,
            (long)(await fixture.CurrentAsync()).Revision)).IsSuccess);
        var (facts, _) = await fixture.FactsAsync();
        var fact = Assert.IsType<TenantSuspendedV2>(facts[^1]);
        var malformed = variant switch
        {
            "tenant" => fact with { TenantId = Uuid.CreateVersion4() },
            "actor" => fact with { OperatorUserId = Uuid.CreateVersion4() },
            _ => fact with
            {
                Operation = fact.Operation with
                { OriginalExpectedRevision = fact.Operation.OriginalExpectedRevision!.Value + 1 }
            },
        };

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() => new AggregateScenario<Tenant>(
            new(fixture.TenantId)).Given([.. facts[..^1], malformed]));

        // Assert
        Assert.Contains("lifecycle fact", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRecoverAcceptedDecisionWithoutDuplicateAppendGivenUnreadableOriginalUnitBody(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(false);
        var id = Uuid.CreateVersion4();
        var revision = (long)(await fixture.CurrentAsync()).Revision;
        var content = new UnreadableUnitContent(fixture.Content, unavailable);
        var handler = new SuspendTenantHandler(new TenantLifecycleOperations(
            new NativeOperationExecutor(fixture.Reader, fixture.Executor, content), content, TimeProvider.System));
        var request = fixture.Command<SuspendTenant>(id, "Reason", revision);

        // Act
        var failed = await handler.HandleAsync(Fixture.Context(request, fixture.Operator, id), CancellationToken.None);
        var accepted = await fixture.CurrentAsync();
        var recovered = await fixture.SendAsync("suspend", id, "Reason", fixture.Operator, revision);
        var after = await fixture.CurrentAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, failed.Error?.Kind);
        Assert.Equal(unavailable, failed.Error!.IsTransient);
        Assert.True(accepted.IsSuspended);
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(accepted.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    sealed class UnreadableUnitContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.Schema != UnitResult.Schema
            ? inner.ReadVerifiedAsync(realm, reference, ct) : throw (unavailable
                ? new ImmutableContentUnavailableException("The original Unit body is temporarily absent.")
                : new ImmutableContentIntegrityException("The original Unit body cannot be verified."));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid Operator { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();

        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        public FitzTenantDirectoryReader Directory() => new(_scope.ServiceProvider.GetRequiredService<IKvClient>());

        public async Task<(DomainEvent[] Facts, EventCursor Cursor)> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            var cursor = EventCursor.Start;
            await foreach (var record in _scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern("bdgrz", "tenants", TenantId.ToString()), cursor, CancellationToken.None))
            {
                facts.Add(record.Event);
                cursor = record.NextCursor;
            }
            return ([.. facts], cursor);
        }

        public Fixture()
        {
            var events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz.invalid/ws",
                ["Fitz:ApplicationName"] = "tenant-lifecycle-tests",
            }).Build(), developerAuthentication: true);
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.RemoveAll<IKvClient>();
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
        }

        public async Task SeedAsync(bool suspended)
        {
            var tenant = new Tenant(TenantId);
            Assert.True(tenant.Register(Operator, "Company", "company").IsSuccess);
            Assert.True(tenant.ConfirmSlug("company").IsSuccess);
            if (suspended)
                Assert.True(tenant.Suspend(Operator, "Earlier suspension", DateTimeOffset.UtcNow).IsSuccess);
            await _scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(tenant,
                new RequestDispatchContext(RequestActor.System));
        }

        public Task<Tenant> CurrentAsync() => Reader.HydrateAsync(new Tenant(TenantId)).AsTask();

        public async ValueTask<Result> SendAsync(string kind, Uuid id, string reason, Uuid actor, long revision)
        {
            var result = await (kind switch
            {
                "suspend" => ActivatorUtilities.CreateInstance<SuspendTenantHandler>(_scope.ServiceProvider)
                    .HandleAsync(Context(Command<SuspendTenant>(id, reason, revision), actor, id), CancellationToken.None),
                "reactivate" => ActivatorUtilities.CreateInstance<ReactivateTenantHandler>(_scope.ServiceProvider)
                    .HandleAsync(Context(Command<ReactivateTenant>(id, reason, revision), actor, id), CancellationToken.None),
                "offboard" => ActivatorUtilities.CreateInstance<OffboardTenantHandler>(_scope.ServiceProvider)
                    .HandleAsync(Context(Command<OffboardTenant>(id, reason, revision), actor, id), CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            });
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        }

        public T Command<T>(Uuid id, string reason, long revision) where T : IRequestBase
        {
            var constructor = typeof(T).GetConstructors().Single();
            return (T)constructor.Invoke(constructor.GetParameters().Select(parameter => parameter.Name switch
            {
                "CommandId" => (object)id,
                "TenantId" => TenantId,
                "Reason" => reason,
                "ExpectedRevision" => revision,
                _ => throw new InvalidOperationException("Unknown lifecycle contract field."),
            }).ToArray());
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid actor, Uuid id) where T : IRequestBase =>
            new(request, new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", actor.ToString()),
                    new Claim("email", "operator@example.test")], "UnitTest")), metadata: new RequestMetadata(id, id)));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
