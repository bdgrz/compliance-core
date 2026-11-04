using System.Runtime.CompilerServices;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Features.Workforce.History;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonLegacyMutationTests
{
    [Theory]
    [InlineData("changed")]
    [InlineData("unchanged")]
    [InlineData("stale")]
    [InlineData("missing")]
    [InlineData("invalid")]
    public async Task ShouldRefuseWithoutFactGivenRetiredGenericPersonMutation(string kind)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var request = new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId,
            kind == "missing" ? Uuid.CreateVersion4() : fixture.PersonId,
            kind == "stale" ? 9 : 1, kind switch
            {
                "unchanged" => "Ada",
                "invalid" => " ",
                _ => "Ada King",
            }, "ada@example.com");
        var before = await FactsAsync(scope.ServiceProvider, new Person(fixture.TenantId, request.PersonId).Stream);
        // Act
        var result = await ((IRequestHandler<RevisePerson, UnitResult>)
            ActivatorUtilities.CreateInstance<RevisePersonHandler>(scope.ServiceProvider))
            .HandleAsync(Context(request, fixture.UserId), CancellationToken.None);

        // Assert
        AssertRetired(result);
        Assert.Equal(before, await FactsAsync(scope.ServiceProvider, new Person(fixture.TenantId, request.PersonId).Stream));
    }

    [Fact]
    public async Task ShouldRefuseWithoutOwnerOrContentExecutionGivenRetiredGenericPersonWriter()
    {
        // Arrange
        var persistence = new ForbiddenPersistence();
        var operations = new PersonOperations(new NativeOperationExecutor(persistence, persistence, persistence),
            new PersonReader(persistence, new InMemoryEventStore(), persistence), persistence, persistence, TimeProvider.System);
        var services = new ServiceCollection();
        services.AddSingleton(operations);
        await using var provider = services.BuildServiceProvider();
        var request = new RevisePerson(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "Ada King");
        Result<UnitResult> result = default;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            result = await ((IRequestHandler<RevisePerson, UnitResult>)
                ActivatorUtilities.CreateInstance<RevisePersonHandler>(provider))
                .HandleAsync(Context(request, Uuid.CreateVersion4()), CancellationToken.None));

        // Assert
        Assert.Null(failure);
        AssertRetired(result);
        Assert.Equal(0, persistence.Calls);
    }

    [Fact]
    public async Task ShouldPreserveHistoricalReceiptPrivacyAndCorrelationGivenNamedChangeAfterSerializedV2Revision()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var contact = new PersonalContactDetails("private@example.test", "+1 202 555 0123");
        var created = new RecordPerson(Uuid.CreateVersion4(), fixture.TenantId, "Historical person", "work@example.test", contact);
        Assert.True((await fixture.BusAsync(created)).IsSuccess);
        Assert.True((await fixture.BusAsync(new CorrelatePersonMembership(Uuid.CreateVersion4(), fixture.TenantId,
            created.PersonId, 1, fixture.UserId))).IsSuccess);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var people = scope.ServiceProvider.GetRequiredService<PersonReader>();
        var legacy = new RevisePerson(Uuid.CreateVersion4(), fixture.TenantId, created.PersonId, 2,
            "Historical corrected person", created.WorkEmail, contact);
        var owner = await reader.HydrateAsync(new Person(fixture.TenantId, created.PersonId, legacy.CommandId));
        var snapshot = await people.ReadAsync(owner);
        var rules = snapshot.Rules;
        var historicalActor = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Historical author");
        var historicalAt = DateTimeOffset.UtcNow;
        // Seed published V2 history directly; the retired command handler does not create this fact.
        Assert.Null(rules.Revise(2, legacy.DisplayName, legacy.WorkEmail, contact, historicalActor, historicalAt));
        var operation = RootOperation.Provenance(legacy.CommandId, nameof(RevisePerson), owner.Stream,
            historicalActor, 2, AuthoredIntentDigest.Compute(legacy), historicalAt) with
        { FirstOwner = owner.Stream };
        var prepared = await PersonContent.FreezeAsync(content, snapshot, rules, operation);
        var firstResponse = await FirstResponses.FreezeAsync(content, owner.Stream.Realm, prepared.Response);
        Assert.True(owner.Append(prepared, operation, firstResponse).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        var historical = Assert.IsType<PersonDetailsRevised>((await FactsAsync(scope.ServiceProvider, owner.Stream))[^1]);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(historical, ComplianceCoreJsonContext.Default.PersonDetailsRevised);
        var roundTripped = JsonSerializer.Deserialize(serialized, ComplianceCoreJsonContext.Default.PersonDetailsRevised)!;
        roundTripped.AttachMetadata(historical.Metadata);
        var exact = await people.ReadFactAsync(roundTripped);
        Assert.Equal(legacy.DisplayName, exact.DisplayName);
        Assert.Equal(contact, exact.PersonalContact);
        Assert.Equal(fixture.UserId, exact.CorrelatedUserId);
        var originalReceipt = (await content.ReadVerifiedAsync(owner.Stream.Realm, historical.FirstResponse)).ToArray();
        var originalResult = (await content.ReadVerifiedAsync(owner.Stream.Realm, historical.OperationResult)).ToArray();
        var named = new ChangePersonDetails(Uuid.CreateVersion4(), fixture.TenantId, created.PersonId, 3,
            "Current person", created.WorkEmail, contact);

        // Act
        var changed = await fixture.BusAsync(named);
        Assert.True(changed.IsSuccess, changed.Error?.Message);
        await fixture.CatchUpAsync();
        var current = await people.ReadAsync(fixture.TenantId, created.PersonId);
        var facts = await FactsAsync(scope.ServiceProvider, owner.Stream);
        var selected = await reader.HydrateAsync(new Person(fixture.TenantId, created.PersonId, legacy.CommandId));
        var receipt = Assert.IsType<PersonDetailsRevised>(selected.Acceptance.SelectedFact);
        var descriptor = await FirstResponses.ReplayAsync(content, owner.Stream.Realm, receipt.FirstResponse);
        var list = await fixture.BusAsync(new ListPeople(fixture.TenantId));
        var retired = await ((IRequestHandler<RevisePerson, UnitResult>)
            ActivatorUtilities.CreateInstance<RevisePersonHandler>(scope.ServiceProvider))
            .HandleAsync(Context(legacy, fixture.UserId), CancellationToken.None);

        // Assert
        Assert.Equal(4, current.Owner.Revision);
        Assert.Equal(named.DisplayName, current.View!.DisplayName);
        Assert.Equal(contact, current.View.PersonalContact);
        Assert.Equal(fixture.UserId, current.View.CorrelatedUserId);
        var namedFact = Assert.IsType<PersonDetailsChanged>(facts[^1]);
        Assert.Equal(new PersonDetailChanges(true, false, false), namedFact.ChangedFields);
        Assert.Equal(nameof(ChangePersonDetails), namedFact.Operation.CommandKind);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), namedFact.Operation.Actor.Id);
        Assert.Equal(namedFact.Operation.Actor, current.View.LastChangedBy);
        Assert.Equal(historical.Operation, receipt.Operation);
        Assert.Equal(historicalActor, receipt.Header.LastChangedBy);
        Assert.Equal(historicalAt, receipt.Header.LastChangedAt);
        Assert.Equal(originalReceipt, (await content.ReadVerifiedAsync(owner.Stream.Realm, receipt.FirstResponse)).ToArray());
        Assert.Equal(originalResult, (await content.ReadVerifiedAsync(owner.Stream.Realm, receipt.OperationResult)).ToArray());
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(UnitResult.Schema, descriptor.ResultSchema);
        var body = Assert.IsType<StandardResponseBody>(descriptor.Body);
        Assert.IsType<UnitResult>(await NativeContent.ReadAsync<UnitResult>(content, fixture.TenantId, body.Content));
        Assert.True(list.IsSuccess, list.Error?.Message);
        var publicPerson = Assert.Single(list.Value.Items, person => person.PersonId == created.PersonId);
        Assert.Null(publicPerson.PersonalContact);
        Assert.True(publicPerson.RestrictedFieldsRedacted);
        AssertRetired(retired);
        Assert.Equal(facts, await FactsAsync(scope.ServiceProvider, owner.Stream));
        foreach (var fault in new[] { "payload", "causation" })
        {
            var altered = fault == "payload"
                ? historical with { Change = historical.Change with { ContentId = Uuid.CreateVersion4() } }
                : historical with { };
            altered.AttachMetadata(fault == "causation"
                ? historical.Metadata with { CausationId = Uuid.CreateVersion4() }
                : historical.Metadata);
            var corrupted = facts.ToArray();
            corrupted[2] = altered;
            await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
                _ = await PersonHistory.ReadAsync(current.Owner, new Records(owner.Stream, corrupted)));
        }
        Assert.Equal(facts, await FactsAsync(scope.ServiceProvider, owner.Stream));
    }

    static void AssertRetired(Result<UnitResult> result)
    {
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.False(error.IsTransient);
        Assert.Equal("Generic person revision is retired; use ChangePersonDetails.", error.Message);
    }

    static Cntryl.Portia.RequestContext<RevisePerson> Context(RevisePerson request, Uuid userId) => new(request,
        new RequestDispatchContext(ProgramManagementServices.Actor(userId)));

    static async Task<DomainEvent[]> FactsAsync(IServiceProvider services, EventStreamAddress stream)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            facts.Add(record.Event);
        return [.. facts];
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => throw new NotSupportedException();
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

    sealed class ForbiddenPersistence : IAggregateReader, IAggregateExecutor, IImmutableContentStore
    {
        public int Calls { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate => ValueTask.FromException<TAggregate>(Unexpected());
        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromException<Result>(Unexpected());
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate => ValueTask.FromException<Result<TOut>>(Unexpected());
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => Task.FromException<ImmutableContentRef>(Unexpected());
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => Task.FromException<ReadOnlyMemory<byte>>(Unexpected());
        InvalidOperationException Unexpected()
        {
            Calls++;
            return new InvalidOperationException("A retired generic writer must not execute owner or content persistence.");
        }
    }
}
