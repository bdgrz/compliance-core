using Bdgrz.Compliance.Features.NativeOperations;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class PersonNativeHistoryTests
{
    [Fact]
    public async Task ShouldRejectCorrelationThatChangesPersonValuesGivenMalformedHistory()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var person = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace");
        Assert.True((await fixture.BusAsync(person)).IsSuccess);
        Assert.True((await CorrelateAsync(fixture, new CorrelatePersonMembership(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 1, fixture.UserId))).IsSuccess);
        var stream = new Person(fixture.TenantId, person.PersonId).Stream;
        var facts = (await FactsAsync(fixture.Provider, stream)).ToArray();
        var correlation = Assert.IsType<PersonMembershipCorrelationRecorded>(facts[1]);
        var altered = correlation with { Header = correlation.Header with { ValuesSha256 = new string('a', 64) } };
        altered.AttachMetadata(correlation.Metadata);
        facts[1] = altered;
        var store = new InMemoryEventStore();
        await store.AppendAsync(stream, 0, facts);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await provider.GetRequiredService<IAggregateReader>().HydrateAsync(new Person(fixture.TenantId, person.PersonId)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldReplayActualEqualCreationWinnerGivenLosingPersonValuePublicationUnavailable()
    {
        // Arrange
        FaultStore store = null!;
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync(
            configureContent: inner => store = new FaultStore(inner));
        var request = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace", "grace@example.com");
        PersonRegistration? original = null;
        store.Winner = async () =>
        {
            var winner = await fixture.BusAsync(request);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            original = winner.Value;
        };
        Result<PersonRegistration>? actual = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await fixture.BusAsync(request));

        // Assert
        Assert.True(store.Failed);
        Assert.NotNull(original);
        Assert.Null(failure);
        Assert.True(actual!.Value.IsSuccess, actual.Value.Error?.Message);
        Assert.Equal(original, actual.Value.Value);
        Assert.Single(await FactsAsync(fixture.Provider, new Person(fixture.TenantId, request.PersonId).Stream));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayEarlierOriginalCreationGivenLaterPersonBodyUnavailableOrCorrupt(bool unavailable)
    {
        // Arrange
        FaultStore store = null!;
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync(
            configureContent: inner => store = new FaultStore(inner));
        var request = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace", "grace@example.com");
        var first = await fixture.BusAsync(request);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var revised = await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId,
            request.PersonId, 1, "Grace Hopper", "grace@example.com"));
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var owner = await reader.HydrateAsync(new Person(fixture.TenantId, request.PersonId));
        store.Blocked = owner.CurrentContent;
        store.Unavailable = unavailable;

        // Act
        var retry = await fixture.BusAsync(request);
        var failure = await Record.ExceptionAsync(async () => _ = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(owner));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        if (unavailable)
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(2, (await FactsAsync(fixture.Provider, owner.Stream)).Count);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("correlation")]
    [InlineData("causation")]
    [InlineData("execution")]
    [InlineData("actor")]
    [InlineData("truncate")]
    public async Task ShouldRefuseDifferentUnselectedIntermediateSavedPersonPrefixGivenCorruptedHistory(string fault)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace");
        Assert.True((await fixture.BusAsync(request)).IsSuccess);
        Assert.True((await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, request.PersonId, 1, "Grace Hopper"))).IsSuccess);
        Assert.True((await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, request.PersonId, 2, "Rear Admiral Grace Hopper"))).IsSuccess);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Person(fixture.TenantId, request.PersonId));
        var facts = (await FactsAsync(fixture.Provider, owner.Stream)).ToArray();
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = Assert.IsType<PersonDetailsRevised>(facts[1]);
            var altered = fault == "payload" ? original with { Change = original.Change with { ContentId = Uuid.CreateVersion4() } } : original with { };
            altered.AttachMetadata(fault switch
            {
                "event_id" => original.Metadata with { EventId = Uuid.CreateVersion4() },
                "correlation" => original.Metadata with { CorrelationId = Uuid.CreateVersion4() },
                "causation" => original.Metadata with { CausationId = Uuid.CreateVersion4() },
                "execution" => original.Metadata with { ExecutionId = Uuid.CreateVersion4() },
                "actor" => original.Metadata with { Actor = new ActorAttribution("another-worker", "bdgrz.system") },
                _ => original.Metadata,
            });
            facts[1] = altered;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await PersonHistory.ReadAsync(owner, new Records(owner.Stream, facts)));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public async Task ShouldFreezeNoChangeCorrelationReceiptGivenSameLinkAtFreshRevision()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var person = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace");
        Assert.True((await fixture.BusAsync(person)).IsSuccess);
        var first = new CorrelatePersonMembership(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 1, fixture.UserId);
        Assert.True((await CorrelateAsync(fixture, first)).IsSuccess);
        var unchanged = new CorrelatePersonMembership(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 2, fixture.UserId);

        // Act
        var accepted = await CorrelateAsync(fixture, unchanged);
        var retry = await CorrelateAsync(fixture, unchanged);
        await fixture.CatchUpAsync();
        var view = await fixture.BusAsync(new GetPerson(fixture.TenantId, person.PersonId));
        var facts = await FactsAsync(fixture.Provider, new Person(fixture.TenantId, person.PersonId).Stream);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.True(view.IsSuccess, view.Error?.Message);
        Assert.Equal(2, view.Value.Revision);
        Assert.Equal(fixture.UserId, view.Value.CorrelatedUserId);
        Assert.Equal(3, facts.Count);
        Assert.False(Assert.IsType<PersonMembershipCorrelationRecorded>(facts[^1]).Changed);
    }

    [Fact]
    public async Task ShouldReauthorizeBeforeOriginalPersonReplayGivenManagementPermissionRevoked()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var request = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace");
        Assert.True((await fixture.BusAsync(request)).IsSuccess);
        fixture.Permissions.Allowed = false;

        // Act
        var retry = await fixture.BusAsync(request);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, retry.Error?.Kind);
        Assert.Single(await FactsAsync(fixture.Provider, new Person(fixture.TenantId, request.PersonId).Stream));
    }

    [Fact]
    public async Task ShouldRefusePreparedPersonMutationGivenCoherentDifferentExecutorSavedMetadataCut()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var person = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Grace");
        Assert.True((await fixture.BusAsync(person)).IsSuccess);
        Assert.True((await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 1, "Grace Hopper"))).IsSuccess);
        Assert.True((await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 2, "Rear Admiral Grace Hopper"))).IsSuccess);
        var facts = (await FactsAsync(fixture.Provider, new Person(fixture.TenantId, person.PersonId).Stream)).ToArray();
        var middle = Assert.IsType<PersonDetailsRevised>(facts[1]);
        var altered = middle with { };
        altered.AttachMetadata(middle.Metadata with { CausationId = Uuid.CreateVersion4() });
        facts[1] = altered;
        var different = new InMemoryEventStore();
        await different.AppendAsync(new Person(fixture.TenantId, person.PersonId).Stream, 0, facts);
        await using var originalScope = fixture.Provider.CreateAsyncScope();
        var content = originalScope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(different);
        services.AddSingleton<IDomainEventReader>(different);
        services.AddSingleton(content);
        services.AddPortia();
        services.AddPersonNativeFixtures();
        await using var actual = services.BuildServiceProvider();
        var originalReader = originalScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var originalEvents = originalScope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var actualReader = actual.GetRequiredService<IAggregateReader>();
        var executor = actual.GetRequiredService<IAggregateExecutor>();
        var operations = new PersonOperations(new NativeOperationExecutor(originalReader, executor, content),
            new PersonReader(originalReader, originalEvents, content), originalReader, content, TimeProvider.System);
        var request = new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, person.PersonId, 3, "Fourth name");
        var context = new Cntryl.Portia.RequestContext<RevisePerson>(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId)));

        // Act
        var refused = await operations.ReviseAsync(context, CancellationToken.None);
        var beforeRetry = await actualReader.HydrateAsync(new Person(fixture.TenantId, person.PersonId));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error?.IsTransient);
        Assert.Equal(3, beforeRetry.Revision);
        var fresh = new PersonOperations(new NativeOperationExecutor(actualReader, executor, content),
            new PersonReader(actualReader, different, content), actualReader, content, TimeProvider.System);
        Assert.True((await fresh.ReviseAsync(context, CancellationToken.None)).IsSuccess);
        Assert.Equal(4, (await actualReader.HydrateAsync(new Person(fixture.TenantId, person.PersonId))).Revision);
    }

    [Fact]
    public async Task ShouldReadAndNativelyReviseExactImmutableLegacyPersonGivenExistingV1History()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var person = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Original author");
        var at = DateTimeOffset.UtcNow.AddDays(-1);
        var contact = new PersonalContactDetails("legacy.private@example.com", "+1 202 555 0147");
        var stream = new Person(fixture.TenantId, person).Stream;
        await using var scope = fixture.Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        await store.AppendAsync(stream, 0,
        [
            DomainEventSeed.Attach(new PersonRecorded(fixture.TenantId, person, "Legacy person", "legacy@example.com", actor, at, contact), person, 1),
            DomainEventSeed.Attach(new PersonMembershipCorrelated(fixture.TenantId, person, 2, fixture.UserId, actor, at.AddMinutes(1)), person, 2),
        ]);
        var people = scope.ServiceProvider.GetRequiredService<PersonReader>();
        var original = await people.ReadAsync(fixture.TenantId, person);

        // Act
        var revised = await ReviseAsync(fixture, new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, person, 2,
            "Revised legacy person", "legacy@example.com", contact));
        var current = await people.ReadAsync(fixture.TenantId, person);
        var facts = await FactsAsync(fixture.Provider, stream);

        // Assert
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.Equal(contact, original.View!.PersonalContact);
        Assert.Equal(fixture.UserId, original.View.CorrelatedUserId);
        Assert.Null(original.Owner.CurrentContent);
        Assert.Equal(2, original.Owner.Revision);
        Assert.Equal(contact, current.View!.PersonalContact);
        Assert.Equal(fixture.UserId, current.View.CorrelatedUserId);
        Assert.Equal("Revised legacy person", current.View.DisplayName);
        Assert.Equal(3, current.Owner.Revision);
        Assert.IsType<PersonRecorded>(facts[0]);
        Assert.IsType<PersonMembershipCorrelated>(facts[1]);
        Assert.IsType<PersonDetailsRevised>(facts[2]);
    }

    static async Task<Result<UnitResult>> ReviseAsync(AttestWorkforceManagementWallTests.Fixture fixture, RevisePerson request)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, ProgramManagementServices.Actor(fixture.UserId));
    }
    static async Task<Result<UnitResult>> CorrelateAsync(AttestWorkforceManagementWallTests.Fixture fixture, CorrelatePersonMembership request)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, ProgramManagementServices.Actor(fixture.UserId));
    }
    static async Task<IReadOnlyList<DomainEvent>> FactsAsync(IServiceProvider services, EventStreamAddress stream)
    {
        await using var scope = services.CreateAsyncScope();
        var facts = new List<DomainEvent>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            facts.Add(record.Event);
        return facts;
    }
    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset, CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) => throw new NotSupportedException();
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset, [EnumeratorCancellation] CancellationToken ct)
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
    sealed class FaultStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Func<Task>? Winner { get; set; }
        public Bdgrz.Compliance.Features.Proofs.ImmutableContentRef? Blocked { get; set; }
        public bool Unavailable { get; set; }
        public bool Failed { get; private set; }
        public async Task<Bdgrz.Compliance.Features.Proofs.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema == PersonContent.ViewSchema && Winner is { } winner)
            {
                Winner = null;
                Failed = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing person candidate body is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, Bdgrz.Compliance.Features.Proofs.ImmutableContentRef reference, CancellationToken ct = default) =>
            reference != Blocked ? inner.ReadVerifiedAsync(realm, reference, ct) : Task.FromException<ReadOnlyMemory<byte>>(Unavailable
                ? new ImmutableContentUnavailableException("The selected person body is unavailable.")
                : new ImmutableContentIntegrityException("The selected person body is corrupt."));
    }
}
