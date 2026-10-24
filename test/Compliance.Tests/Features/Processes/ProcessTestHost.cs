using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

/// <summary>Composes the process kernel over in-memory Fitz KV with a scripted receiver.</summary>
sealed class ProcessTestHost : IAsyncDisposable
{
    public const string Process = "ProbeProcess";
    public const string Realm = "tenant-a";
    public static readonly Uuid Root = Uuid.Parse("00000000-0000-4000-8000-0000000000d1",
        System.Globalization.CultureInfo.InvariantCulture);
    public static readonly Uuid SourceEvent = Uuid.Parse("00000000-0000-4000-8000-0000000000d2",
        System.Globalization.CultureInfo.InvariantCulture);
    public static readonly NativeFactReference Source = new(new EventStreamAddress(Realm, "probes", "p1"), SourceEvent, 1);
    public static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('1', 64), 10,
        "application/json", "probe-intent.v1");

    public ProcessTestHost(Func<IProcessRecordStore, IProcessRecordStore>? store = null,
        ProcessDefinition? definition = null)
    {
        var services = new ServiceCollection();
        Kv = new InMemoryKvClient();
        Recorder = new ProbeReceiveRecorder();
        Clock = new ManualClock(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));
        IProcessRecordStore records = new FitzKvProcessRecordStore(new PersistenceKvClient(Kv));
        Store = store?.Invoke(records) ?? records;
        services.AddSingleton(Recorder);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(Store);
        services.AddSingleton(definition ?? Definition());
        services.AddSingleton<ProcessCatalog>(provider => new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        services.AddSingleton<ProcessSweep>();
        services.AddPortia().AddRequestHandler<ProbeReceiveHandler>().AddRequestAuthorizer<ProbeReceiveHandler>();
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public ServiceProvider Provider { get; }

    public InMemoryKvClient Kv { get; }

    public ProbeReceiveRecorder Recorder { get; }

    public ManualClock Clock { get; }

    public IProcessRecordStore Store { get; }

    public static ProcessKey Key => ProcessKey.ForRoot(Process, Root, Source);

    /// <summary>acquire[AcquireX] per target, apply[ApplyA, ApplyB], settle[SettleX] per target; abort[RejectX].</summary>
    public static ProcessDefinition Definition() => new(Process, ["ProbeAdmitted"], true,
    [
        new ProcessStageDefinition("acquire", ["AcquireX"]),
        new ProcessStageDefinition("apply", ["ApplyA", "ApplyB"]),
        new ProcessStageDefinition("settle", ["SettleX"]),
    ], ["RejectX"], step => new ProbeReceive(step.CommandId, step.Receiver, step.Target));

    public async Task<ProcessRecord> StartAsync(params string[] targets)
    {
        return await Store.CreateIfAbsentAsync(ProcessRecord.Start(Key, Source, "initial", Intent, targets,
            Clock.GetUtcNow()));
    }

    public async Task<ProcessRecord> RunAsync(string worker = "w1")
    {
        await using var scope = Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>()
            .RunAsync(Key, worker, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    public sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
