using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the three invitation owners end to end over an in-memory event store and in-memory Fitz: the
///     invitation, mailbox claim and delivery process, the four command handlers, the three delivery receivers on the
///     real request bus, the process kernel and the test source-fact resolver. A test submits commands as callers,
///     then drives the delivery process the way its root-fact reactor would.
/// </summary>
sealed class TenantInvitationHost : IAsyncDisposable
{
    public static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    readonly Uuid[] _knownPrograms;

    public TenantInvitationHost(Func<ITenantInvitationDeliveryStore, ITenantInvitationDeliveryStore>? deliveryStore = null,
        RecordingInvitationDelivery? mailbox = null, params Uuid[] programs)
    {
        _knownPrograms = programs;
        Mailbox = mailbox ?? new RecordingInvitationDelivery();
        Clock = new HostClock(At);
        Keys = EmailChallengeTokenKeys.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build(), true);
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        var kv = new InMemoryKvClient();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(Keys);
        services.AddSingleton<ITenantInvitationDelivery>(Mailbox);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new PersistenceKvClient(kv)));
        services.AddSingleton<IProcessRecordStore>(new FitzKvProcessRecordStore(new PersistenceKvClient(kv)));
        ITenantInvitationDeliveryStore deliveries = new FitzKvTenantInvitationDeliveryStore(new PersistenceKvClient(kv));
        services.AddSingleton(deliveryStore?.Invoke(deliveries) ?? deliveries);
        services.AddSingleton(TenantInvitationDeliveryProcess.Definition);
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddSingleton<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddScoped<TenantInvitationAdmission>();
        services.AddScoped<TenantInvitationMailboxes>();
        services.AddScoped<TenantInvitationDeliverySupport>();
        services.AddSingleton<IProgramResourceScopeResolver>(new KnownPrograms(this));
        services.AddPortia()
            .AddRequestHandler<InviteOrganizationMemberHandler>()
            .AddRequestHandler<InviteGuestConsultantHandler>()
            .AddRequestHandler<AcceptTenantInvitationHandler>()
            .AddRequestHandler<SendTenantInvitationHandler>()
            .AddRequestHandler<RecordTenantInvitationDeliverySentHandler>()
            .AddRequestHandler<RecordTenantInvitationDeliveryFailedHandler>()
            .AddRequestAuthorizer<TenantInvitationDeliveryReceiverAuthorizer>();
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Events = events;
        Content = Provider.GetRequiredService<IImmutableContentStore>();
        Processes = Provider.GetRequiredService<IProcessRecordStore>();
        Deliveries = Provider.GetRequiredService<ITenantInvitationDeliveryStore>();
    }

    public ServiceProvider Provider { get; }

    public InMemoryEventStore Events { get; }

    public IImmutableContentStore Content { get; }

    public IProcessRecordStore Processes { get; }

    public ITenantInvitationDeliveryStore Deliveries { get; }

    public RecordingInvitationDelivery Mailbox { get; }

    public HostClock Clock { get; }

    public EmailChallengeTokenKeys Keys { get; }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public static ClaimsPrincipal Caller(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    // --- public commands

    public async Task<Result> InviteMemberAsync(Uuid invitationId, string email,
        string role = BuiltInRbac.ComplianceManagementRole, Uuid? inviter = null, Uuid? tenantId = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await new InviteOrganizationMemberHandler(scope.ServiceProvider.GetRequiredService<TenantInvitationAdmission>())
            .HandleAsync(new RequestContext<InviteOrganizationMember>(new InviteOrganizationMember(invitationId,
                tenantId ?? TenantId, email, role), Caller(inviter ?? Inviter)), CancellationToken.None);
    }

    public async Task<Result> InviteGuestAsync(Uuid invitationId, string email, IReadOnlyList<Uuid> programIds,
        DateOnly expiresOn, Uuid? inviter = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await new InviteGuestConsultantHandler(scope.ServiceProvider.GetRequiredService<TenantInvitationAdmission>(),
            Provider.GetRequiredService<IProgramResourceScopeResolver>()).HandleAsync(
            new RequestContext<InviteGuestConsultant>(new InviteGuestConsultant(invitationId, TenantId, email,
                programIds, expiresOn), Caller(inviter ?? Inviter)), CancellationToken.None);
    }

    public async Task<Result> AcceptAsync(Uuid invitationId, string email, string token, Uuid userId,
        Uuid? tenantId = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return await new AcceptTenantInvitationHandler(services.GetRequiredService<NativeOperationExecutor>(),
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<TenantInvitationMailboxes>(),
            Content, Clock).HandleAsync(new RequestContext<AcceptTenantInvitation>(new AcceptTenantInvitation(
            invitationId, tenantId ?? TenantId, email, token), Caller(userId)), CancellationToken.None);
    }

    /// <summary>The user who authors invitations unless a test names another.</summary>
    public Uuid Inviter { get; } = Uuid.CreateVersion4();

    /// <summary>Appends an invitation fact directly, as a historical or foreign-key invitation, without its mailbox claim.</summary>
    public async Task SeedInvitationAsync(Uuid invitationId, TenantInvitationTerms terms, string token, string? keyId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new TenantInvitation(TenantId, invitationId, invitationId),
            invitation => AggregateOutcome.CommitOnSuccess(invitation.Invite(invitationId, terms, token, At,
                At.AddDays(7), 1, null, keyId)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    // --- facts

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

    public Task<List<DomainEvent>> InvitationFactsAsync(Uuid invitationId) =>
        FactsAsync(new TenantInvitation(TenantId, invitationId).Stream);

    public Task<List<DomainEvent>> MailboxFactsAsync(string email) =>
        FactsAsync(new TenantInvitationMailboxClaim(TenantId, email).Stream);

    public async Task<TenantMemberInvited> InvitedAsync(Uuid invitationId) =>
        (await InvitationFactsAsync(invitationId)).OfType<TenantMemberInvited>().Single();

    public async Task<TenantInvitationMailboxClaim> MailboxAsync(string email)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TenantInvitationMailboxClaim(TenantId, email));
    }

    /// <summary>The bearer token the invitation delivers, derived exactly as the sender derives it.</summary>
    public async Task<string> TokenOfAsync(Uuid invitationId)
    {
        var invited = await InvitedAsync(invitationId);
        return Keys.DeriveInvitation(invited.TokenKeyId!, invitationId, TenantId, invited.EmailAddress, invited.ExpiresAt);
    }

    public async Task<DomainEventRecord> InvitedRecordAsync(Uuid invitationId)
    {
        await foreach (var record in Events.ReadAsync(new TenantInvitation(TenantId, invitationId).Stream, 0))
        {
            if (record.Event is TenantMemberInvited)
                return record;
        }

        throw new InvalidOperationException($"Invitation {invitationId} has no invitation fact.");
    }

    // --- the delivery process

    public Task<ProcessKey> ProcessKeyOf(Uuid invitationId) =>
        ProcessKeys.FindAsync(Provider, TenantInvitationDeliveryProcess.Name, TenantId.ToString(), invitationId);

    public async Task<ProcessRecord?> ProcessAsync(Uuid invitationId) => await Processes.ReadAsync(await ProcessKeyOf(invitationId));

    /// <summary>Starts and drives the delivery process of one invitation, exactly as its root-fact reactor would.</summary>
    public async Task<ProcessRecord> DriveDeliveryAsync(Uuid invitationId)
    {
        var record = await InvitedRecordAsync(invitationId);
        var invited = (TenantMemberInvited)record.Event;
        var payload = await Content.WriteJsonAsync(TenantId.ToString(),
            Uuid.CreateVersion5(invitationId, TenantInvitationDeliveryPayload.Schema), TenantInvitationDeliveryPayload.Schema,
            TenantInvitationDeliveryPayload.Of(invited));
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessRootReaction>().StartAsync(
            TenantInvitationDeliveryProcess.Name, new RecordReactorContext(record), payload, [], "test");
    }

    /// <summary>Resumes an existing delivery process record, as the sweep would.</summary>
    public async Task<ProcessRecord> ResumeDeliveryAsync(Uuid invitationId, string worker = "sweep")
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(await ProcessKeyOf(invitationId), worker,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Sends one receiver command through the real bus as the given principal (the system process by default).</summary>
    public async Task<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command, ClaimsPrincipal? actor = null)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command,
            new RequestDispatchContext(actor ?? RequestActor.System), CancellationToken.None);
    }

    /// <summary>Builds the exact receiver command the driver would send for a step of the process record.</summary>
    public async Task<IRequest<ReceiverOutcome>?> CommandAsync(ProcessRecord record, string stage, string receiver)
    {
        var step = new ProcessStep(record, stage, receiver, null, EffectIdentity.CommandId(record.Source.EventId,
            $"{record.Key.ProcessKind}:{receiver}", "-", record.Episode));
        await using var scope = Provider.CreateAsyncScope();
        return await TenantInvitationDeliveryProcess.Definition.CreateReceiver(step, scope.ServiceProvider,
            CancellationToken.None);
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    sealed class KnownPrograms(TenantInvitationHost host) : IProgramResourceScopeResolver
    {
        public ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == host.TenantId && host._knownPrograms.Contains(programId));

        public ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request,
            CancellationToken ct = default) => ValueTask.FromResult<Uuid?>(null);
    }

    public sealed class HostClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
