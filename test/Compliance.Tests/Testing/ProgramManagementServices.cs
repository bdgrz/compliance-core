using System.Security.Claims;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Features.Workforce.Services;
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
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Boundaries.Readers;
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
        IServiceCollection collection = new ServiceCollection();
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
        collection.AddScoped<ProgramReader>();
        collection.AddScoped<ProgramOperations>();
        collection.AddScoped<ProductClientServiceReader>();
        collection.AddScoped<ProductClientServiceOperations>();
        collection.AddScoped<IndependenceRuleCatalogReader>();
        collection.AddScoped<IndependenceRuleRatificationReader>();
        collection.AddScoped<IndependenceRuleCatalogOperations>();
        collection.AddScoped<Bdgrz.Compliance.Features.AccessReviews.Services.AccessReviewManifests>();
        collection.AddScoped<Bdgrz.Compliance.Features.AccessReviews.Readers.AccessReviewReader>();
        collection.AddScoped<ClientManagementIndependenceGuard>();
        collection.AddScoped<Bdgrz.Compliance.Features.PolicyDistribution.Rules.PolicyAcknowledgementRecorderGuard>();
        collection.AddScoped<LegacyWorkAssignmentHistory>();
        collection.AddScoped<WorkAssignmentReader>();
        collection.AddScoped<WorkAssignmentAuthorization>();
        collection.AddScoped<WorkAssignmentOperations>();
        collection.AddScoped<WorkDigestHistoryReader>();
        collection.AddScoped<WorkDigestRetryOperations>();
        collection.AddScoped<ControlOperationsReader>();
        collection.AddScoped<ProviderSource>();
        collection.AddScoped<ControlMappingReader>();
        collection.AddScoped<ApplicabilityReader>();
        collection.AddScoped<ControlEvaluationReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>();
        collection.AddScoped<Bdgrz.Compliance.Features.Readiness.Readers.ReadinessReader>();
        collection.AddScoped<ControlReader>();
        collection.AddScoped<ControlOperations>();
        collection.AddScoped<BoundaryReader>();
        collection.AddScoped<BoundaryOperations>();
        collection.AddScoped<RiskDraftReader>();
        collection.AddScoped<RiskDraftOperations>();
        collection.AddScoped<RiskEvaluationReader>();
        collection.AddScoped<RiskEvaluationOperations>();
        collection.AddScoped<CommitmentReader>();
        collection.AddScoped<CommitmentOperations>();
        collection.AddScoped<PolicyReader>();
        collection.AddScoped<PolicyOperations>();
        collection.AddScoped<RiskMethodReader>();
        collection.AddScoped<PersonReader>();
        collection.AddScoped<PersonOperations>();
        services?.Invoke(collection);
        handlers(collection.AddPortia().AddRequestAuthorizer<ProgramManagementAuthorizer>());
        collection.AddVerifiedSourceUseReader();
        collection.AddVerifiedPolicyCampaignReader();
        collection.AddVerifiedAuditorRequestReader();
        collection.AddVerifiedAccessReviewReader();
        collection.AddVerifiedEvaluationReader();
        collection.AddVerifiedMappingLineReader();
        collection.AddVerifiedPracticeCommitmentReader();
        collection.AddVerifiedServiceEngagementReader();
        collection.AddVerifiedWorkDigestReader();
        collection.AddVerifiedWorkAssignmentReader();
        collection.AddVerifiedProgramReader();
        collection.AddVerifiedProductClientServiceReader();
        collection.AddVerifiedWorkPreferenceReader();
        collection.AddPersonNativeFixtures();
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static async Task SeedAsync(IServiceProvider provider, ProductClientServiceRules selected,
        Func<ProductClientServiceRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var rules = await services.GetRequiredService<ProductClientServiceReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ProductClientServiceSeeds.AppendAsync(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, ProductClientServiceRules selected,
        Func<ProductClientServiceRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var rules = await services.GetRequiredService<ProductClientServiceReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ProductClientServiceSeeds.AppendAsync(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static Task SeedAsync(IServiceProvider provider, ProgramRules selected,
        Func<ProgramRules, Bdgrz.Compliance.Features.Versioning.Models.CommandFailure?> operation) =>
        SeedAsync(provider, selected, rules =>
        {
            var failure = operation(rules);
            Assert.Null(failure);
            return Result.Success;
        });

    public static async Task SeedAsync(IServiceProvider provider, ProgramRules selected, Func<ProgramRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var rules = await services.GetRequiredService<ProgramReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ProgramSeeds.AppendAsync(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task<ProgramRules> HydrateAsync(IServiceProvider provider, ProgramRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProgramReader>().RulesAsync(selected.TenantId, selected.Id);
    }

    public static async Task<RiskMethodRules> HydrateAsync(IServiceProvider provider, RiskMethodRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<RiskMethodReader>()
            .ReadAsync(selected.TenantId, selected.ProgramId);
        return RiskMethodRules.FromVersions(selected.TenantId, selected.ProgramId, snapshot.Versions);
    }

    public static async Task SeedAsync(IServiceProvider provider, RiskMethodRules selected,
        Func<RiskMethodRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var rules = await HydrateAsync(provider, selected);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskMethodSeeds.AppendAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync(IServiceProvider provider, IndependenceRuleCatalogRules selected,
        Func<IndependenceRuleCatalogRules, Result> operation)
    {
        ArgumentNullException.ThrowIfNull(selected);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = (await services.GetRequiredService<IndependenceRuleCatalogReader>().ReadAsync()).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await IndependenceCatalogSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }
    public static async Task SeedAsync(IServiceProvider provider, IndependenceRuleRatificationRules selected,
        Func<IndependenceRuleRatificationRules, Result> operation)
    {
        ArgumentNullException.ThrowIfNull(selected);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = (await services.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync()).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await IndependenceCatalogSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }
    public static async Task SeedAsync<TOut>(IServiceProvider provider, IndependenceRuleCatalogRules selected,
        Func<IndependenceRuleCatalogRules, Result<TOut>> operation)
    {
        ArgumentNullException.ThrowIfNull(selected);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = (await services.GetRequiredService<IndependenceRuleCatalogReader>().ReadAsync()).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await IndependenceCatalogSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules);
    }
    public static async Task SeedAsync<TOut>(IServiceProvider provider, IndependenceRuleRatificationRules selected,
        Func<IndependenceRuleRatificationRules, Result<TOut>> operation)
    {
        ArgumentNullException.ThrowIfNull(selected);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = (await services.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync()).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await IndependenceCatalogSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules);
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

    public static async Task SeedAsync(IServiceProvider provider, BoundaryRules selected,
        Func<BoundaryRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<BoundaryReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await BoundarySeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, BoundaryRules selected,
        Func<BoundaryRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<BoundaryReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await BoundarySeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(),
            services.GetRequiredService<IImmutableContentStore>(), rules, CancellationToken.None);
    }

    public static async Task<BoundaryRules> HydrateAsync(IServiceProvider provider, BoundaryRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BoundaryReader>()
            .RulesAsync(selected.TenantId, selected.Id, CancellationToken.None);
    }

    public static async Task SeedAsync(IServiceProvider provider, RiskDraftRules selected,
        Func<RiskDraftRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<RiskDraftReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskDraftSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, RiskDraftRules selected,
        Func<RiskDraftRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<RiskDraftReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskDraftSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task<RiskDraftRules> HydrateAsync(IServiceProvider provider, RiskDraftRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RiskDraftReader>().RulesAsync(selected.TenantId, selected.Id);
    }

    public static async Task SeedAsync(IServiceProvider provider, RiskEvaluationRules selected,
        Func<RiskEvaluationRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<RiskEvaluationReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskEvaluationSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, RiskEvaluationRules selected,
        Func<RiskEvaluationRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var rules = await services.GetRequiredService<RiskEvaluationReader>().RulesAsync(selected.TenantId, selected.Id);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await RiskEvaluationSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task<RiskEvaluationRules> HydrateAsync(IServiceProvider provider, RiskEvaluationRules selected)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RiskEvaluationReader>().RulesAsync(selected.TenantId, selected.Id);
    }

    public static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "author@example.com")], "BdgrzSession"));

    public static IProgramResourceScopeResolver ResourceScopes(Uuid? programId = null,
        bool applicationExists = true) =>
        new TestProgramResourceScopeResolver(programId, applicationExists);

    public static async Task SeedAsync(IServiceProvider provider, ControlCriterionMappingRules selected,
        Func<ControlCriterionMappingRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<ControlMappingReader>();
        var owner = await reader.ReadLineAsync(selected.TenantId, selected.ProgramId, selected.ControlId, selected.EditionId, selected.CriterionIdentifier);
        var rules = await reader.ComposeAsync(owner);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await MappingNativeSeeds.AppendAsync(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync(IServiceProvider provider, CriterionApplicabilityRules selected,
        Func<CriterionApplicabilityRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<ApplicabilityReader>();
        var owner = await reader.ReadLineAsync(selected.TenantId, selected.ProgramId, selected.EditionId, selected.CriterionIdentifier);
        var rules = await reader.ComposeAsync(owner);
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await MappingNativeSeeds.AppendAsync(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateWriter>(), services.GetRequiredService<IImmutableContentStore>(), rules);
    }

    public static async Task SeedAsync(IServiceProvider provider, PersonRules selected, Func<PersonRules, Result> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var rules = (await new PersonReader(reader, events, content).ReadAsync(selected.TenantId, selected.Id)).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await PersonSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), events, content, rules);
    }

    public static async Task SeedAsync<TOut>(IServiceProvider provider, PersonRules selected, Func<PersonRules, Result<TOut>> operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var rules = (await new PersonReader(reader, events, content).ReadAsync(selected.TenantId, selected.Id)).Rules;
        var result = operation(rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await PersonSeeds.AppendAsync(reader, services.GetRequiredService<IAggregateWriter>(), events, content, rules);
    }

    public static Task SeedAsync(IServiceProvider provider, PersonRules selected, Func<PersonRules, Bdgrz.Compliance.Features.Versioning.Models.CommandFailure?> operation) =>
        SeedAsync(provider, selected, person =>
        {
            var failure = operation(person);
            return failure is null ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));
        });

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
