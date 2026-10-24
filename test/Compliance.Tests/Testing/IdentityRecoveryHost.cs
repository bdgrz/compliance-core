using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes identity recovery end to end over in-memory Fitz and an in-memory event store: the two public
///     routes, the ten receivers on the real request bus, the process kernel and the test source-fact resolver.
///     A test starts and completes a recovery, then drives the process when it wants the owners to move, which is
///     how a paused receiver, a competing association or a crash is exercised.
/// </summary>
sealed class IdentityRecoveryHost : IAsyncDisposable
{
    public const string OldProvider = "https://old-issuer.example/";
    public const string NewProvider = "https://new-issuer.example/";
    public const string Email = "person@example.com";

    readonly EmailChallengeTokenKeys _keys = CreateTokenKeys();

    public EmailChallengeTokenKeys Keys => _keys;

    public IdentityRecoveryHost(Func<IProcessRecordStore, IProcessRecordStore>? store = null)
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
        IProcessRecordStore records = new FitzKvProcessRecordStore(new PersistenceKvClient(kv));
        services.AddSingleton(store?.Invoke(records) ?? records);
        Delivery = new RecordingDelivery();
        services.AddSingleton<IEmailChallengeDelivery>(Delivery);
        services.AddSingleton(IdentityRecoveryProcess.Definition);
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddSingleton<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<IdentityRecoveryReceiverSupport>();
        services.AddPortia()
            .AddRequestHandler<MaterializeIdentityRecoveryPreparationHandler>()
            .AddRequestHandler<ProtectRecoveryIdentityHandler>()
            .AddRequestHandler<AcceptProtectedIdentityRecoveryHandler>()
            .AddRequestHandler<RegisterRecoveredIdentityHandler>()
            .AddRequestHandler<RevokeRecoveredOldIdentityHandler>()
            .AddRequestHandler<CompleteAcceptedIdentityRecoveryHandler>()
            .AddRequestHandler<SettleRecoveryIdentityProtectionHandler>()
            .AddRequestHandler<CompleteEmailRecoveryChallengeHandler>()
            .AddRequestHandler<SendIdentityRecoveryNoticeHandler>()
            .AddRequestHandler<RejectIdentityRecoveryPreparationHandler>()
            .AddRequestAuthorizer<IdentityRecoveryReceiverAuthorizer>();
        Provider = services.BuildServiceProvider();
        Events = events;
        Content = Provider.GetRequiredService<IImmutableContentStore>();
        Store = Provider.GetRequiredService<IProcessRecordStore>();
    }

    public ProcessTestHost.ManualClock Clock { get; }

    public ServiceProvider Provider { get; }

    public InMemoryEventStore Events { get; }

    public IImmutableContentStore Content { get; }

    public IProcessRecordStore Store { get; }

    public RecordingDelivery Delivery { get; }

    public IAggregateReader Reader => Provider.GetRequiredService<IAggregateReader>();

    /// <summary>An account with one registered provider identity and a verified email address.</summary>
    public async Task<Account> SeedAccountAsync(string email = Email, string subject = "old-subject")
    {
        var userId = Uuid.CreateVersion4();
        await SeedAsync(new UserIdentity(OldProvider, subject), identity => identity.Register(userId, email));
        var now = DateTimeOffset.UtcNow;
        const string proof = "verified-email-proof";
        await SeedAsync(new EmailAddress(email), address =>
        {
            var reserved = address.Reserve(userId);
            if (!reserved.IsSuccess)
                return reserved;
            var issued = address.IssueChallenge(userId, Uuid.CreateVersion4(),
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof))), now.AddMinutes(15), now);
            return issued.IsSuccess ? address.CompleteChallenge(userId, proof, now) : issued;
        });
        return new Account(userId, email, UserIdentity.GetIdentityId(OldProvider, subject));
    }

    public async Task<Result> StartAsync(Uuid challengeId, string email = Email) =>
        await StartHandler().HandleAsync(new RequestContext<StartIdentityRecovery>(
            new StartIdentityRecovery(challengeId, email), new ClaimsPrincipal(new ClaimsIdentity())),
            CancellationToken.None);

    /// <summary>The bearer token the recovery email carries; derived exactly as the delivery reactor derives it.</summary>
    public async Task<string> TokenAsync(Account account, Uuid challengeId)
    {
        var issued = (await FactsAsync(new EmailAddress(account.Email).Stream)).OfType<IdentityRecoveryChallengeIssued>()
            .Single(fact => fact.ChallengeId == challengeId);
        return _keys.DeriveRecovery(issued.TokenKeyId, challengeId, account.UserId, account.Email, issued.ExpiresAt);
    }

    /// <summary>Starts a recovery and returns the token its email carries.</summary>
    public async Task<string> StartedAsync(Account account, Uuid challengeId)
    {
        Assert.True((await StartAsync(challengeId, account.Email)).IsSuccess);
        return await TokenAsync(account, challengeId);
    }

    public async Task<Result<OperationAcceptedResponse>> CompleteAsync(Account account, Uuid challengeId, string token,
        string provider = NewProvider, string subject = "new-subject", Uuid? oldIdentityId = null,
        string? assertedEmail = null) =>
        await CompleteHandler().HandleAsync(new RequestContext<CompleteIdentityRecovery>(
            new CompleteIdentityRecovery(account.Email, challengeId, token, oldIdentityId ?? account.OldIdentityId),
            ProviderActor(provider, subject, assertedEmail)), CancellationToken.None);

    public static ClaimsPrincipal ProviderActor(string issuer, string subject, string? email = null)
    {
        var claims = new List<Claim> { new("iss", issuer), new("sub", subject) };
        if (email is not null)
            claims.Add(new Claim("email", email));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc"));
    }

    StartIdentityRecoveryHandler StartHandler() => new(Operations(), Reader, _keys, Content, Clock);

    CompleteIdentityRecoveryHandler CompleteHandler() => new(Operations(), Reader, Content, Clock);

    NativeOperationExecutor Operations() =>
        new(Reader, Provider.GetRequiredService<IAggregateExecutor>(), Content);

    // --- the process

    /// <summary>The claim fact of a challenge as the root reactor would see it.</summary>
    public async Task<DomainEventRecord> ClaimRecordAsync(Account account, Uuid? challengeId = null)
    {
        await foreach (var record in Events.ReadAsync(new EmailAddress(account.Email).Stream, 0))
        {
            if (record.Event is IdentityRecoveryChallengeClaimed claimed &&
                (challengeId is null || claimed.ChallengeId == challengeId))
                return record;
        }

        throw new InvalidOperationException("No matching claim was recorded.");
    }

    /// <summary>Starts and drives the process of one claimed challenge, exactly as its root-fact reactor would.</summary>
    public async Task<ProcessRecord> DriveAsync(Account account, string worker = "test", Uuid? challengeId = null)
    {
        var record = await ClaimRecordAsync(account, challengeId);
        var claimed = (IdentityRecoveryChallengeClaimed)record.Event;
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            IdentityRecoveryProcess.Name, new RecordReactorContext(record), claimed.Intent,
            [claimed.OldIdentityId.ToString(), claimed.ReplacementIdentityId.ToString()], worker);
    }

    /// <summary>Creates the process record without driving it, as the reactor does before its first dispatch.</summary>
    public async Task<ProcessRecord> PrepareProcessAsync(Account account, Uuid? challengeId = null)
    {
        var record = await ClaimRecordAsync(account, challengeId);
        var claimed = (IdentityRecoveryChallengeClaimed)record.Event;
        return await Store.CreateIfAbsentAsync(ProcessRecord.Start(ProcessKeyOf(claimed.ChallengeId),
            NativeFactReference.Of(record.Stream, record.Event), claimed.Operation.Episode, claimed.Intent,
            [claimed.OldIdentityId.ToString(), claimed.ReplacementIdentityId.ToString()], DateTimeOffset.UtcNow));
    }

    /// <summary>Builds the exact receiver command the driver would send for a step of the record.</summary>
    public async Task<IRequest<ReceiverOutcome>> CommandAsync(ProcessRecord record, string receiver, string? target = null)
    {
        var stage = IdentityRecoveryProcess.Definition.Stages.FirstOrDefault(s => s.Receivers.Contains(receiver))?.Stage
            ?? "abort";
        var step = new ProcessStep(record, stage, receiver, target, EffectIdentity.CommandId(record.Source.EventId,
            $"{record.Key.ProcessKind}:{receiver}", target ?? "-", record.Episode));
        return await IdentityRecoveryProcess.Definition.CreateReceiver(step, Provider, CancellationToken.None)
            ?? throw new InvalidOperationException($"{receiver} is not selected for this record.");
    }

    /// <summary>Resumes an existing process record, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeAsync(Uuid challengeId, string worker = "sweep")
    {
        // A crashed worker leaves its lease behind; the sweep resumes the record once the lease lapsed.
        Clock.Advance(ProcessDriver.LeaseDuration + TimeSpan.FromSeconds(1));
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(ProcessKeyOf(challengeId), worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public static ProcessKey ProcessKeyOf(Uuid challengeId) =>
        new(IdentityRecoveryProcess.Name, IdentityRecoveryReceiverSupport.Realm, challengeId);

    public Task<ProcessRecord?> ProcessAsync(Uuid challengeId) => Store.ReadAsync(ProcessKeyOf(challengeId));

    /// <summary>Sends one receiver command through the real bus as the system principal.</summary>
    public async Task<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command, ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        return await bus.SendAsync(command, new RequestDispatchContext(actor ?? RequestActor.System),
            CancellationToken.None);
    }

    // --- reading the owners

    public async Task<IdentityRecovery> RecoveryAsync(Uuid challengeId) =>
        await Reader.HydrateAsync(new IdentityRecovery(challengeId));

    public async Task<UserIdentity> IdentityAsync(Uuid identityId) => await Reader.HydrateAsync(new UserIdentity(identityId));

    public async Task<EmailAddress> AddressAsync(string email = Email) => await Reader.HydrateAsync(new EmailAddress(email));

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

    public async Task<IdentityRecoveryIntent> IntentAsync(Account account, Uuid? challengeId = null)
    {
        var claimed = (IdentityRecoveryChallengeClaimed)(await ClaimRecordAsync(account, challengeId)).Event;
        return await Content.ReadJsonAsync<IdentityRecoveryIntent>(IdentityRecoveryReceiverSupport.Realm, claimed.Intent);
    }

    public async Task SeedAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, Result> operation)
        where TAggregate : Aggregate
    {
        var result = await Provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    /// <summary>Seeds an operation that answers a value, such as a registration.</summary>
    public Task SeedAsync<TAggregate, TValue>(TAggregate aggregate, Func<TAggregate, Result<TValue>> operation)
        where TAggregate : Aggregate where TValue : notnull =>
        SeedAsync(aggregate, item =>
        {
            var result = operation(item);
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        });

    /// <summary>Another account registers the provider identity directly, as a competing association.</summary>
    public async Task<Uuid> RegisterElsewhereAsync(string provider, string subject)
    {
        var other = Uuid.CreateVersion4();
        await SeedAsync(new UserIdentity(provider, subject), identity => identity.Register(other, null));
        return other;
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    static EmailChallengeTokenKeys CreateTokenKeys()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "current",
            ["Compliance:EmailDelivery:TokenKeys:current"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }).Build();
        return EmailChallengeTokenKeys.FromConfiguration(configuration, false);
    }

    /// <summary>One account that owns an email address and a registered identity.</summary>
    public sealed record Account(Uuid UserId, string Email, Uuid OldIdentityId);

    /// <summary>Records completion notices and fails them on demand.</summary>
    public sealed class RecordingDelivery : IEmailChallengeDelivery
    {
        public int Failures { get; set; }

        public List<(Uuid ChallengeId, Uuid UserId, string Email)> Notices { get; } = [];

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token, CancellationToken ct) =>
            ValueTask.CompletedTask;

        public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
            CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId, string emailAddress,
            CancellationToken ct)
        {
            if (Failures > 0)
            {
                Failures--;
                throw new InvalidOperationException("The mail server is unavailable.");
            }

            Notices.Add((challengeId, userId, emailAddress));
            return ValueTask.CompletedTask;
        }
    }
}
