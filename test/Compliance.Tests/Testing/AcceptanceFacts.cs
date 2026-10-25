using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds the paired personal decisions through the real owners and processes, and drives admitted acceptances and
///     staff assignments through their ordered process, as the reactor and sweep do.
/// </summary>
static class AcceptanceFacts
{
    /// <summary>Records and materializes the paired management acknowledgement and, for a conditional history, the partner evaluation.</summary>
    public static async Task PairedDecisionsAsync(IndependenceHost host, Uuid engagement, PartnerSetup partner,
        long engagementRevision = 1, bool evaluate = true)
    {
        var acknowledge = await AcknowledgementAsync(host, engagement, engagementRevision);
        Assert.True((await host.HttpAsync(acknowledge)).IsSuccess);
        var acknowledged = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, engagement, acknowledge.AcknowledgementId);
        Assert.Equal(ProcessState.Completed, acknowledged.State);
        if (!evaluate)
            return;
        var evaluation = await EvaluationAsync(host, engagement, partner, engagementRevision);
        Assert.True((await host.HttpAsync(evaluation, partner.Actor)).IsSuccess);
        var evaluated = await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant, engagement, evaluation.EvaluationId);
        Assert.Equal(ProcessState.Completed, evaluated.State);
    }

    public static ProcessKey KeyOf(Uuid operation, NativeFactReference source) =>
        ProcessKey.ForRoot(EngagementAcceptanceEffects.ProcessName, operation, source);

    /// <summary>The admitted acceptance or assignment of an engagement, or a failure when it is not admitted.</summary>
    public static Task<EngagementAcceptanceRecord> OperationAsync(IndependenceHost host, Uuid engagement, Uuid operation) =>
        OperationAsync(host.Provider, host.Tenant, engagement, operation);

    /// <summary>The admitted acceptance or assignment of an engagement in any composition.</summary>
    public static async Task<EngagementAcceptanceRecord> OperationAsync(IServiceProvider provider, Uuid tenant,
        Uuid engagement, Uuid operation)
    {
        await using var scope = provider.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenant, engagement, selectedRootId: operation)))
            .OperationOf(operation) ?? throw new InvalidOperationException("Admit first.");
    }

    /// <summary>The canonical acquisition keys of the frozen manifests of an admitted operation, as the reactor derives them.</summary>
    public static Task<IReadOnlyList<string>> TargetsAsync(IndependenceHost host, Uuid engagement, Uuid operation) =>
        TargetsAsync(host.Provider, host.Tenant, engagement, operation);

    /// <summary>The canonical acquisition keys of an admitted operation in any composition.</summary>
    public static async Task<IReadOnlyList<string>> TargetsAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid operation)
    {
        var admitted = await OperationAsync(provider, tenant, engagement, operation);
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(tenant.ToString(), admitted.Intent);
        return EngagementAcceptanceProcess.Targets(
            await content.ReadSelectionAsync<SourceUseRequirement>(tenant.ToString(), intent.SourceUses),
            await content.ReadSelectionAsync<PracticeSubjectIntent>(tenant.ToString(), intent.Subjects));
    }

    /// <summary>Creates (if absent) the process record of an admitted operation without dispatching any receiver.</summary>
    public static Task<ProcessRecord> StartAsync(IndependenceHost host, Uuid engagement, Uuid operation) =>
        StartAsync(host.Provider, host.Tenant, engagement, operation);

    /// <summary>Creates (if absent) the process record of an admitted operation in any composition.</summary>
    public static async Task<ProcessRecord> StartAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid operation)
    {
        var admitted = await OperationAsync(provider, tenant, engagement, operation);
        var targets = await TargetsAsync(provider, tenant, engagement, operation);
        await using var scope = provider.CreateAsyncScope();
        var stream = new ServiceEngagementIndependence(tenant, engagement).Stream;
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(
            ProcessRecord.Start(KeyOf(operation, NativeFactReference.Of(stream, admitted.Requested)), NativeFactReference.Of(stream, admitted.Requested),
                admitted.RequestedOperation.Operation.Episode, admitted.Intent, targets, DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the process of an admitted operation until it ends or waits.</summary>
    public static Task<ProcessRecord> RunAsync(IndependenceHost host, Uuid engagement, Uuid operation) =>
        RunAsync(host.Provider, host.Tenant, engagement, operation);

    /// <summary>Starts (if absent) and drives the process of an admitted operation in any composition.</summary>
    public static async Task<ProcessRecord> RunAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid operation)
    {
        var started = await StartAsync(provider, tenant, engagement, operation);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Requests an abort of the process of an admitted operation, as an operator or the sweep would.</summary>
    public static async Task<ProcessRecord> AbortAsync(IndependenceHost host, Uuid engagement, Uuid operation)
    {
        var started = await StartAsync(host, engagement, operation);
        await using var scope = host.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().AbortAsync(started.Key);
    }

    /// <summary>Builds the receiver command of one step of the process, as the driver would for that record.</summary>
    public static async Task<IRequest<ReceiverOutcome>?> BuildAsync(IndependenceHost host, ProcessRecord record,
        string receiver, string? target = null)
    {
        await using var scope = host.Provider.CreateAsyncScope();
        var definition = scope.ServiceProvider.GetRequiredService<ProcessCatalog>().Get(record.Key.ProcessKind);
        var stage = receiver is nameof(RejectServiceEngagementAcceptance) or nameof(RejectServiceEngagementStaffAssignment)
            ? "abort"
            : definition.Stages.Single(item => item.Receivers.Contains(receiver)).Stage;
        var step = new ProcessStep(record, stage, receiver, target,
            EngagementAcceptanceEffects.ReceiverCommandId(record.Source.EventId, receiver, target, record.Episode));
        return await definition.CreateReceiver(step, scope.ServiceProvider, CancellationToken.None);
    }

    /// <summary>Creates the draft of an engagement led by a new staff member, without recording any service.</summary>
    public static async Task<Uuid> CreateEngagementAsync(IndependenceHost host, string practice = "attest")
    {
        var lead = await host.RegisterStaffAsync(practice);
        var engagement = Uuid.CreateVersion4();
        var created = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)));
        Assert.True(created.IsSuccess, created.Error?.Message);
        return engagement;
    }

    /// <summary>
    ///     Creates an open-ended attest or advisory engagement and records the conditionally compatible readiness
    ///     service, so a partner evaluation is required for an attest acceptance.
    /// </summary>
    public static async Task<Uuid> SeedOpenEngagementAsync(IndependenceHost host, string practice = "attest")
    {
        var lead = await host.RegisterStaffAsync(practice);
        var engagement = Uuid.CreateVersion4();
        var created = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.OpenDraft(lead)));
        Assert.True(created.IsSuccess, created.Error?.Message);
        var recorded = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            ReadinessService()));
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return engagement;
    }

    /// <summary>Accepts an open-ended engagement through its paired decisions and process; returns the accepting partner.</summary>
    public static async Task<(Uuid Engagement, PartnerSetup Partner, Uuid Operation)> AcceptedAsync(
        IndependenceHost host, string practice = "attest")
    {
        var partner = await SeedPartnerAsync(host, null, practice);
        var engagement = await SeedOpenEngagementAsync(host, practice);
        await PairedDecisionsAsync(host, engagement, partner, evaluate: practice == "attest");
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await RunAsync(host, engagement, command.CommandId);
        Assert.Equal(ProcessState.Completed, record.State);
        return (engagement, partner, command.CommandId);
    }

    /// <summary>
    ///     Runs the root reactor of the acceptance process over the committed facts of the host's client, as the
    ///     production worker does: it starts the process from each admission and drives it under the reactor principal.
    /// </summary>
    public static async Task RunRootReactorAsync(IndependenceHost host)
    {
        await using var scope = host.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registration = Assert.Single(services.GetServices<WorkloadRegistration>(),
            item => item.Name == EngagementAcceptanceReactor.WorkloadName);
        Assert.Equal(WorkloadScope.PerTenant, registration.Scope);
        var reactor = (Reactor)services.GetRequiredService(registration.ComponentType);
        await new ReactorScenario(new TenantId(host.Tenant.ToString())).RunAsync(reactor);
        var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
        var checkpoint = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
        var runner = new ReactorRunner(services.GetRequiredService<IDomainEventReader>(),
            services.GetRequiredService<IReactorPrincipalProvider>(), services.GetRequiredService<TimeProvider>());
        while (true)
        {
            var next = await runner.RunAsync(reactor, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    /// <summary>The process record of an operation, or null when no process was started for it.</summary>
    public static async Task<ProcessRecord?> RecordOfAsync(IndependenceHost host, Uuid engagement, Uuid operation)
    {
        await using var scope = host.Provider.CreateAsyncScope();
        var admitted = await OperationAsync(host, engagement, operation);
        var source = NativeFactReference.Of(new ServiceEngagementIndependence(host.Tenant, engagement).Stream,
            admitted.Requested);
        return await scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().ReadAsync(KeyOf(operation, source));
    }

    /// <summary>The acceptance command for the exact current revision of the engagement.</summary>
    public static AcceptServiceEngagement AcceptCommand(IndependenceHost host, Uuid engagement, long revision = 1) =>
        new(Uuid.CreateVersion4(), host.Tenant, engagement, revision);

    /// <summary>
    ///     Seeds ratified rules, a current engagement partner and an attest engagement with its complete paired
    ///     decisions, so the next step is the acceptance itself.
    /// </summary>
    public static async Task<(Uuid Engagement, PartnerSetup Partner)> ReadyAsync(IndependenceHost host,
        string practice = "attest", IndependenceRuleContent? rules = null)
    {
        var partner = await SeedPartnerAsync(host, rules, practice);
        var (engagement, _) = await SeedEngagementAsync(host, practice);
        await PairedDecisionsAsync(host, engagement, partner, evaluate: practice == "attest");
        return (engagement, partner);
    }

    /// <summary>Dispatches a receiver command as the trusted system caller.</summary>
    public static Task<Result<ReceiverOutcome>> SendAsync(IndependenceHost host, IRequest<ReceiverOutcome> command) =>
        SendReceiverAsync(host.Provider, command);

    public static ClaimsPrincipal Actor(PartnerSetup partner) => partner.Actor;
}
