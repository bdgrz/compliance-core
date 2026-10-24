using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Controls;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
/// Composes the smallest provider that runs a program-management request through Portia's real
/// lifecycle: the production <see cref="ProgramManagementAuthorizer"/> over an active tenant and
/// a member actor, an in-memory event store, and the handlers the test registers.
/// </summary>
static class ProgramManagementServices
{
    public static ServiceProvider Build(IPermissionAuthorizer permissions,
        Action<PortiaBuilder> handlers, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        var eventStore = new InMemoryEventStore();
        collection.AddSingleton<IEventStore>(eventStore);
        collection.AddSingleton<IDomainEventReader>(eventStore);
        collection.AddSingleton(permissions);
        collection.AddSingleton<IAccessGrantPermissionAuthorizer>(
            new PermissionBackedAccessGrantPermissionAuthorizer(permissions));
        collection.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        collection.AddSingleton<ITenantActivity, ActiveTenant>();
        collection.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        collection.AddSingleton(TimeProvider.System);
        collection.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        collection.AddScoped<FirmStaffViews>();
        collection.AddScoped<NativeOperationExecutor>();
        collection.AddScoped<ControlOperationsReader>();
        collection.AddScoped<ProviderSource>();
        collection.AddScoped<ControlMappingReader>();
        collection.AddScoped<ApplicabilityReader>();
        collection.AddScoped<ControlEvaluationReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Risks.RiskGovernanceReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Evidence.EvidenceRequestReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Readiness.ReadinessReader>();
        collection.AddScoped<ControlReader>();
        collection.AddScoped<ControlOperations>();
        collection.AddScoped<CommitmentReader>();
        collection.AddScoped<CommitmentOperations>();
        collection.AddScoped<PolicyReader>();
        collection.AddScoped<PolicyOperations>();
        services?.Invoke(collection);
        handlers(collection.AddPortia().AddRequestAuthorizer<ProgramManagementAuthorizer>());
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static async Task SeedAsync(IServiceProvider provider, ControlRules selected,
        Func<ControlRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<ControlReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ControlSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, ControlRules selected,
        Func<ControlRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<ControlReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ControlSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task<ControlRules> HydrateAsync(IServiceProvider provider, ControlRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ControlReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
    }

    public static async Task SeedAsync(IServiceProvider provider, PolicyRules selected,
        Func<PolicyRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<PolicyReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await PolicySeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, PolicyRules selected,
        Func<PolicyRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<PolicyReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await PolicySeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task<PolicyRules> HydrateAsync(IServiceProvider provider, PolicyRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PolicyReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
    }

    public static async Task SeedAsync(IServiceProvider provider, CommitmentRules selected,
        Func<CommitmentRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<CommitmentReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await CommitmentSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, CommitmentRules selected,
        Func<CommitmentRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<CommitmentReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await CommitmentSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task<CommitmentRules> HydrateAsync(IServiceProvider provider, CommitmentRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommitmentReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
    }

    public static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "author@example.com")], "BdgrzSession"));

    public static IProgramResourceScopeResolver ResourceScopes(Uuid? programId = null,
        bool applicationExists = true) =>
        new TestProgramResourceScopeResolver(programId, applicationExists);

    public static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate aggregate,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    public static async Task SeedAsync<TAggregate, TOut>(IServiceProvider provider,
        TAggregate aggregate, Func<TAggregate, Result<TOut>> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    public static async Task<TAggregate> HydrateAsync<TAggregate>(IServiceProvider provider,
        TAggregate aggregate) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(aggregate, CancellationToken.None);
    }
}

sealed class TestProgramResourceScopeResolver(Uuid? programId = null, bool applicationExists = true)
    : IProgramResourceScopeResolver
{
    public ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid requestedProgramId,
        CancellationToken ct = default) => ValueTask.FromResult(true);

    public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request,
        CancellationToken ct = default) => ValueTask.FromResult(programId);

    public ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default) => ValueTask.FromResult(applicationExists);
}
