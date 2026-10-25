using System.Security.Claims;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Features.Processes;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the email challenge delivery process end to end over in-memory Fitz and an in-memory event store: the
///     address handlers, the process kernel, the delivery worker and its two outcome commands on the real request bus,
///     with a scripted mail relay. A test issues a challenge, drives the process the way the root reactor would, and
///     scripts how the relay answers each attempt.
/// </summary>
sealed class EmailChallengeDeliveryHost : IAsyncDisposable
{
    public const string Email = "person@example.com";

    readonly EmailChallengeTokenKeys _keys = CreateTokenKeys();

    public EmailChallengeDeliveryHost(Func<IEmailChallengeDeliveryLedger, IEmailChallengeDeliveryLedger>? ledger = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        Clock = new ProcessTestHost.ManualClock(DateTimeOffset.UtcNow);
        services.AddSingleton<TimeProvider>(Clock);
        var kv = new InMemoryKvClient();
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)));
        services.AddSingleton<IProcessRecordStore>(new FitzKvProcessRecordStore(new PersistenceKvClient(kv)));
        IEmailChallengeDeliveryLedger inner = new FitzKvEmailChallengeDeliveryLedger(new PersistenceKvClient(kv));
        services.AddSingleton(ledger?.Invoke(inner) ?? inner);
        Relay = new ScriptedRelay();
        services.AddSingleton<IEmailChallengeDelivery>(Relay);
        services.AddSingleton(_keys);
        services.AddSingleton(EmailChallengeDeliveryProcess.Definition);
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<EmailChallengeDeliveryWorker>();
        services.AddScoped<Bdgrz.Compliance.Features.NativeOperations.NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<DeliverEmailChallengeHandler>()
            .AddRequestHandler<DeliverRecoveryChallengeHandler>()
            .AddRequestAuthorizer<EmailChallengeDeliveryReceiverAuthorizer>()
            .AddRequestHandler<RecordEmailChallengeDeliverySentHandler>()
            .AddRequestHandler<RecordEmailChallengeDeliveryFailedHandler>()
            .AddRequestAuthorizer<EmailChallengeDeliveryOutcomeAuthorizer>();
        services.AddVerifiedIdentityOwnerReader();
        Provider = services.BuildServiceProvider();
        Events = events;
        Content = Provider.GetRequiredService<IImmutableContentStore>();
        Store = Provider.GetRequiredService<IProcessRecordStore>();
        Ledger = Provider.GetRequiredService<IEmailChallengeDeliveryLedger>();
    }

    public ProcessTestHost.ManualClock Clock { get; }

    public ServiceProvider Provider { get; }

    public InMemoryEventStore Events { get; }

    public IImmutableContentStore Content { get; }

    public IProcessRecordStore Store { get; }

    public IEmailChallengeDeliveryLedger Ledger { get; }

    public ScriptedRelay Relay { get; }

    public EmailChallengeTokenKeys Keys => _keys;

    /// <summary>An owner with a reserved address that has one issued verification challenge.</summary>
    public async Task<(Uuid Owner, Uuid ChallengeId, DomainEventRecord Record)> IssueVerificationAsync(
        string email = Email)
    {
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        var expiresAt = Clock.GetUtcNow().AddMinutes(15);
        var token = _keys.Derive(_keys.ActiveKeyId, challengeId, owner, email, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        await SeedAsync(new EmailAddress(email), address =>
        {
            var reserved = address.Reserve(owner);
            return reserved.IsSuccess
                ? address.IssueChallenge(owner, challengeId, hash, expiresAt, Clock.GetUtcNow(), _keys.ActiveKeyId)
                : reserved;
        });
        return (owner, challengeId, await FirstRecordAsync<EmailChallengeIssued>(new EmailAddress(email).Stream));
    }

    /// <summary>An owner with a verified address that has one issued recovery challenge.</summary>
    public async Task<(Uuid Owner, Uuid ChallengeId, DomainEventRecord Record)> IssueRecoveryAsync(string email = Email)
    {
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        var expiresAt = Clock.GetUtcNow().AddMinutes(15);
        var token = _keys.DeriveRecovery(_keys.ActiveKeyId, challengeId, owner, email, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        const string proof = "previously-verified";
        await SeedAsync(new EmailAddress(email), address =>
        {
            var now = Clock.GetUtcNow();
            var reserved = address.Reserve(owner);
            if (!reserved.IsSuccess)
                return reserved;
            var issued = address.IssueChallenge(owner, Uuid.CreateVersion4(),
                Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(proof))), now.AddMinutes(15), now);
            if (!issued.IsSuccess)
                return issued;
            var verified = address.CompleteChallenge(owner, proof, now);
            return verified.IsSuccess
                ? address.IssueRecovery(owner, challengeId, hash, expiresAt, now, _keys.ActiveKeyId)
                : verified;
        });
        return (owner, challengeId, await FirstRecordAsync<IdentityRecoveryChallengeIssued>(new EmailAddress(email).Stream));
    }

    /// <summary>The token a verification email carries, derived exactly as the delivery worker derives it.</summary>
    public async Task<string> VerificationTokenAsync(Uuid owner, Uuid challengeId, string email = Email)
    {
        var issued = (await FactsAsync(new EmailAddress(email).Stream)).OfType<EmailChallengeIssued>()
            .Single(fact => fact.ChallengeId == challengeId);
        return _keys.Derive(issued.TokenKeyId, challengeId, owner, email, issued.ExpiresAt);
    }

    /// <summary>Starts the process of a root fact and drives it once, exactly as the root reactor would.</summary>
    public async Task<ProcessRecord> DriveAsync(DomainEventRecord record, string worker = "test")
    {
        var intent = record.Event switch
        {
            EmailChallengeIssued issued => Intent(issued.Operation, issued.ChallengeId, issued.UserId,
                EmailChallengeDeliveryIntent.Verification, issued.ExpiresAt, issued.TokenKeyId),
            IdentityRecoveryChallengeIssued issued => Intent(issued.Operation, issued.ChallengeId, issued.UserId,
                EmailChallengeDeliveryIntent.Recovery, issued.ExpiresAt, issued.TokenKeyId),
            _ => throw new InvalidOperationException("Not a challenge fact."),
        };
        var frozen = await Content.WriteJsonAsync(EmailChallengeDeliveryWorker.Realm,
            Uuid.CreateVersion5(record.Event.Metadata.EventId, EmailChallengeDeliveryIntent.Schema),
            EmailChallengeDeliveryIntent.Schema, intent);
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            EmailChallengeDeliveryProcess.Name, new RecordReactorContext(record), frozen, [], worker);
    }

    /// <summary>Resumes the process of a challenge after its lease and retry delay lapsed, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeAsync(Uuid challengeId, string worker = "sweep")
    {
        Clock.Advance(ProcessDriver.LeaseDuration + ProcessDriver.RetryDelay + TimeSpan.FromSeconds(1));
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(await ProcessKeyOf(challengeId), worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public Task<ProcessKey> ProcessKeyOf(Uuid challengeId) =>
        ProcessKeys.FindAsync(Provider, EmailChallengeDeliveryProcess.Name, EmailChallengeDeliveryWorker.Realm, challengeId);

    public async Task<ProcessRecord?> ProcessAsync(Uuid challengeId) => await Store.ReadAsync(await ProcessKeyOf(challengeId));

    /// <summary>Sends one command through the real bus as the given principal (the system process by default).</summary>
    public async Task<Result> SendAsync(IRequest command, ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command,
            new RequestDispatchContext(actor ?? RequestActor.System), CancellationToken.None);
    }

    public async Task<List<DomainEvent>> FactsAsync(EventStreamAddress stream)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in Events.ReadAsync(stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    public async Task<DomainEventRecord> FirstRecordAsync<TFact>(EventStreamAddress stream) where TFact : DomainEvent
    {
        await foreach (var record in Events.ReadAsync(stream, 0))
        {
            if (record.Event is TFact)
                return record;
        }

        throw new InvalidOperationException($"No {typeof(TFact).Name} on {stream}.");
    }

    public async Task SeedAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, Result> operation)
        where TAggregate : Aggregate
    {
        var result = await Provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    static EmailChallengeDeliveryIntent Intent(Bdgrz.Compliance.Features.NativeOperations.OperationProvenance operation,
        Uuid challengeId, Uuid userId, string purpose, DateTimeOffset expiresAt, string tokenKeyId) =>
        new(new Bdgrz.Compliance.Features.NativeOperations.DurableIntentHeader(operation.RootOperationId, operation.Owner,
            operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
            operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt), challengeId, userId,
            purpose, expiresAt, tokenKeyId);

    static EmailChallengeTokenKeys CreateTokenKeys()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "current",
            ["Compliance:EmailDelivery:TokenKeys:current"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }).Build();
        return EmailChallengeTokenKeys.FromConfiguration(configuration, false);
    }

    /// <summary>One message handed to the relay.</summary>
    public sealed record Message(Uuid ChallengeId, string Email, string Token, bool Recovery);

    /// <summary>A mail relay whose answer to each send is scripted; an unscripted send is accepted.</summary>
    public sealed class ScriptedRelay : IEmailChallengeDelivery
    {
        readonly Queue<Exception?> _script = new();

        /// <summary>Every send, whatever the relay answered.</summary>
        public List<Message> Sends { get; } = [];

        /// <summary>Only the sends the relay accepted.</summary>
        public List<Message> Accepted { get; } = [];

        /// <summary>Scripts the answer to the next send: null accepts, an exception rejects or loses the answer.</summary>
        public void Then(Exception? answer) => _script.Enqueue(answer);

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
            CancellationToken ct) => Handle(new Message(challengeId, emailAddress, token, false));

        public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
            CancellationToken ct) => Handle(new Message(challengeId, emailAddress, token, true));

        public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId, string emailAddress,
            CancellationToken ct) => ValueTask.CompletedTask;

        ValueTask Handle(Message message)
        {
            Sends.Add(message);
            if (_script.TryDequeue(out var answer) && answer is not null)
                throw answer;
            Accepted.Add(message);
            return ValueTask.CompletedTask;
        }
    }
}
