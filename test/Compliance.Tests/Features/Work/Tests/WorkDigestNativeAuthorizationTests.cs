using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkDigestNativeAuthorizationTests
{
    [Fact]
    public async Task ShouldPermitSameEvidenceWithFreshCommandGivenAnotherUnknownAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var first = fixture.Request();
        var original = await fixture.InvokeAsync(first);
        Assert.True(original.IsSuccess, original.Error?.Message);
        await fixture.MutateAsync(owner => owner.StartAttempt(fixture.Week, fixture.At.AddMinutes(4)));
        await fixture.MutateAsync(owner => owner.RecordUnknown(fixture.Week, fixture.At.AddMinutes(5), "ambiguous_again"));

        // Act
        var later = await fixture.InvokeAsync(first with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 6 });
        fixture.Clock.Set(fixture.At.AddDays(8));
        var replay = await fixture.InvokeAsync(first);

        // Assert
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(2, later.Value.Attempts);
        Assert.Equal(7, later.Value.Revision);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(original.Value, replay.Value);
        Assert.Equal(4, replay.Value.Revision);
        Assert.Equal(1, replay.Value.Attempts);
        Assert.Equal(2, (await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>().Count());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("rationale")]
    [InlineData("evidence")]
    [InlineData("revision")]
    [InlineData("week")]
    [InlineData("whitespace")]
    public async Task ShouldRefuseChangedBindingBeforeContentReadsGivenAlreadyAcceptedRetryCommand(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        Assert.True((await fixture.InvokeAsync(request)).IsSuccess);
        var different = changed switch
        {
            "rationale" => request with { Rationale = "Another rationale" },
            "evidence" => request with { EvidenceReference = "receipt:another" },
            "revision" => request with { ExpectedRevision = 4 },
            "week" => request with { WeekOf = request.WeekOf.AddDays(7) },
            "whitespace" => request with { EvidenceReference = request.EvidenceReference + " " },
            _ => request
        };
        var reads = fixture.Content.Reads;
        fixture.Content.ReadFailure = new ImmutableContentUnavailableException("No immutable reads should be necessary for a binding conflict.");

        // Act
        var result = await fixture.InvokeAsync(different, changed == "actor" ? Uuid.CreateVersion4() : null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Equal(reads, fixture.Content.Reads);
        Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
    }

    [Fact]
    public async Task ShouldRecoverOneOriginalAdmissionGivenCommittedAppendResponseWasLost()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        var handler = fixture.Handler(new CommitThenFailOnceExecutor(fixture.Executor));

        // Act
        var result = await fixture.InvokeAsync(request, handler: handler);
        var replay = await fixture.InvokeAsync(request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(result.Value, replay.Value);
        Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
    }

    [Fact]
    public async Task ShouldRecoverEqualWinnerGivenCandidatePublicationFailsAfterCompetingAdmission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        WorkDigestDispatchStatusView? winningStatus = null;
        fixture.Content.BeforeNextWrite = async () =>
        {
            var winner = await fixture.InvokeAsync(request);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            winningStatus = winner.Value;
        };
        fixture.Content.NextWriteFailure = new ImmutableContentUnavailableException("Candidate publication response was lost.");

        // Act
        var recovered = await fixture.InvokeAsync(request);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(winningStatus, recovered.Value);
        Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyOriginalContentFailureGivenRetryAlreadyAccepted(bool missing)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        Assert.True((await fixture.InvokeAsync(request)).IsSuccess);
        fixture.Content.ReadFailure = missing ? new ImmutableContentUnavailableException("Original result is not visible yet.") :
            new ImmutableContentIntegrityException("Original result is corrupt.");

        // Act
        var result = await fixture.InvokeAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(missing, result.Error?.IsTransient);
        Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAppendNothingGivenCandidateContentIsUnavailableOrCorruptWithoutWinner(bool missing)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Content.NextWriteFailure = missing ? new ImmutableContentUnavailableException("Candidate is unavailable.") :
            new ImmutableContentIntegrityException("Candidate is corrupt.");

        // Act
        var result = await fixture.InvokeAsync(fixture.Request());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(missing, result.Error?.IsTransient);
        Assert.Empty((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldEnforceCurrentOperatorAuthorityBeforeOriginalResponseReadsGivenAcceptedCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        Assert.True((await fixture.InvokeAsync(request)).IsSuccess);
        var reads = fixture.Content.Reads;
        fixture.Operators.Allowed = false;

        // Act
        var denied = await fixture.InvokeAsync(request);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(reads, fixture.Content.Reads);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("owner")]
    [InlineData("revision")]
    [InlineData("actor")]
    [InlineData("intent")]
    [InlineData("before")]
    [InlineData("result")]
    [InlineData("metadata_actor")]
    [InlineData("legacy_unqualified")]
    public async Task ShouldRejectContradictoryNativeAuthorizationGivenExactStoredSourceTuple(string fault)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        Assert.True((await fixture.InvokeAsync(request)).IsSuccess);
        var fact = Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
        var changed = fault switch
        {
            "root" => fact with { Operation = fact.Operation with { RootOperationId = Uuid.CreateVersion4() } },
            "owner" => fact with { Operation = fact.Operation with { FirstOwner = new EventStreamAddress(fixture.Tenant.ToString(), WorkDigestDispatch.Area, Uuid.CreateVersion4().ToString()) } },
            "revision" => fact with { Operation = fact.Operation with { OriginalExpectedRevision = 2 } },
            "actor" => fact with { AuthorizedBy = Uuid.CreateVersion4() },
            "intent" => fact with { Rationale = "Another valid rationale" },
            "result" => fact with { OperationResult = fact.ResponseContent },
            "legacy_unqualified" => fact with { Operation = null!, Change = null!, FirstResponse = null, OperationResult = null },
            _ => fact with { }
        };
        if (fault == "before")
        {
            var original = await fixture.Content.ReadJsonAsync<WorkDigestRetryAuthorizationContent>(fixture.Tenant.ToString(), fact.Change);
            var wrong = original with { Before = original.Before with { MessageId = Uuid.CreateVersion4() } };
            changed = fact with
            {
                Change = await fixture.Content.WriteJsonAsync(fixture.Tenant.ToString(), Uuid.CreateVersion4(),
                WorkDigestRetryContent.ChangeSchema, wrong)
            };
        }
        changed.AttachMetadata(fault == "metadata_actor" ? fact.Metadata with
        {
            Actor = fact.Metadata.Actor! with { Subject = Uuid.CreateVersion4().ToString() }
        } : fact.Metadata);
        var raw = new AlteredEvents(fixture.Events, record => record.Event.Metadata.EventId == fact.Metadata.EventId ?
            record with { Event = changed } : record);
        var owner = await fixture.Reader.HydrateAsync(new WorkDigestDispatch(fixture.Tenant, fixture.Member, fixture.Week, request.CommandId));

        // Act
        var failure = await Record.ExceptionAsync(async () => await WorkDigestDispatchHistory.ReadAsync(raw, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.IsNotType<ImmutableContentUnavailableException>(failure);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly AsyncServiceScope _scope;
        public ServiceProvider Provider { get; }
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public Uuid Operator { get; } = Uuid.CreateVersion4();
        public DateOnly Week { get; } = new(2026, 10, 12);
        public DateTimeOffset At { get; } = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public MutableClock Clock { get; }
        public OperatorAccess Operators { get; } = new();
        public ProbeContent Content { get; } = new(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public IDomainEventReader Events => _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        public Fixture()
        {
            Clock = new MutableClock(At.AddMinutes(3));
            Provider = ProgramManagementServices.Build(new Permissions(), portia => portia
                .AddRequestHandler<AuthorizeWorkDigestUnknownRetryHandler>()
                .AddRequestAuthorizer<PlatformOperatorAuthorizer>(), services =>
                {
                    services.AddSingleton<IPlatformOperatorAccess>(Operators);
                    services.AddSingleton<TimeProvider>(Clock);
                    services.AddSingleton<IImmutableContentStore>(Content);
                    services.AddSingleton(new WorkDigestDeliverySettings("https://app.example",
                        "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
                        4, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1)));
                });
            _scope = Provider.CreateAsyncScope();
        }
        public AuthorizeWorkDigestUnknownRetry Request() => new(Uuid.CreateVersion4(), Tenant, Member, Week,
            "receipt:original", "Verified external receipt", 3);
        public Task SeedAsync() => WorkDigestFacts.DispatchAsync(Provider, Tenant, Member, rules =>
            rules.Schedule(new WorkDigestScheduleWindow(Week, "UTC", At), Uuid.CreateVersion4(), TimeSpan.FromDays(1), At) &&
            rules.StartAttempt(Week, At.AddMinutes(1)) && rules.RecordUnknown(Week, At.AddMinutes(2), "ambiguous"));
        public async Task MutateAsync(Func<WorkDigestDispatch, bool> mutation)
        {
            var result = await Executor.ExecuteAsync(new WorkDigestDispatch(Tenant, Member), owner =>
            {
                Assert.True(mutation(owner));
                return AggregateOutcome.Commit(Result.Success);
            }, new Cntryl.Portia.RequestContext<Mutation>(new Mutation(), RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        public AuthorizeWorkDigestUnknownRetryHandler Handler(IAggregateExecutor executor) => new(new WorkDigestRetryOperations(
            new NativeOperationExecutor(Reader, executor, Content), Content, Clock,
            _scope.ServiceProvider.GetRequiredService<WorkDigestDeliverySettings>()));
        public async Task<Result<WorkDigestDispatchStatusView>> InvokeAsync(AuthorizeWorkDigestUnknownRetry request,
            Uuid? actorId = null, AuthorizeWorkDigestUnknownRetryHandler? handler = null)
        {
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", (actorId ?? Operator).ToString())], "BdgrzSession"));
            var dispatch = new RequestDispatchContext(actor, new HttpInvocation("PUT", "/fixture/retry", "/fixture/retry", "fixture"));
            var authorization = await new PlatformOperatorAuthorizer(Operators).AuthorizeAsync(
                new Cntryl.Portia.RequestContext<IPlatformOperatorRequest>(request, dispatch), CancellationToken.None);
            if (!authorization.IsSuccess)
                return Result<WorkDigestDispatchStatusView>.Failure(authorization.Error!);
            return await (handler ?? _scope.ServiceProvider.GetRequiredService<AuthorizeWorkDigestUnknownRetryHandler>()).HandleAsync(
                new Cntryl.Portia.RequestContext<AuthorizeWorkDigestUnknownRetry>(request, dispatch), CancellationToken.None);
        }
        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(new WorkDigestDispatch(Tenant, Member).Stream, 0, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    facts.Add(record.Event);
            return facts;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }
    sealed record Mutation : IRequest;
    sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset now) => _now = now;
    }
    sealed class OperatorAccess : IPlatformOperatorAccess
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class ProbeContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Exception? ReadFailure { get; set; }
        public Exception? NextWriteFailure { get; set; }
        public Func<Task>? BeforeNextWrite { get; set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            var action = BeforeNextWrite;
            var failure = NextWriteFailure;
            BeforeNextWrite = null;
            NextWriteFailure = null;
            if (action is not null)
                await action();
            if (failure is not null)
                throw failure;
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            Reads++;
            return ReadFailure is { } failure ? Task.FromException<ReadOnlyMemory<byte>>(failure) : inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                yield return alter(record);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return alter(record);
        }
    }
}
