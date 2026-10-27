using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Registers the coverage projections over an in-memory KV store and catches them up from the
///     in-memory event store, as the per-tenant projectors do on a worker host.
/// </summary>
static class CoverageProjections
{
    public static IServiceCollection AddCoverageProjections(this IServiceCollection services)
    {
        var kv = new InMemoryKvClient();
        services.AddSingleton<IDomainEventReader>(provider =>
            (IDomainEventReader)provider.GetRequiredService<IEventStore>());
        services.AddSingleton(provider => new FitzControlMappingDirectoryV1(kv, provider.GetRequiredService<IImmutableContentStore>()));
        services.AddSingleton<IControlMappingDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddSingleton(provider => new FitzCriterionApplicabilityDirectoryV1(kv, provider.GetRequiredService<IImmutableContentStore>()));
        services.AddSingleton<ICriterionApplicabilityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<CriteriaCoverageReadConsistency>();
        return services;
    }

    public static async Task CatchUpAsync(IServiceProvider provider, Uuid tenantId)
    {
        var events = provider.GetRequiredService<IDomainEventReader>();
        var mappings = provider.GetRequiredService<FitzControlMappingDirectoryV1>();
        await CatchUpAsync(events, tenantId, FitzControlMappingDirectoryV1.ProjectorName,
            FitzControlMappingDirectoryV1.SourceArea,
            await mappings.LoadCheckpointAsync(tenantId), mappings.BeginAsync,
            mappings.ApplyAsync);
        var decisions = provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>();
        await CatchUpAsync(events, tenantId, FitzCriterionApplicabilityDirectoryV1.ProjectorName,
            FitzCriterionApplicabilityDirectoryV1.SourceArea,
            await decisions.LoadCheckpointAsync(tenantId), decisions.BeginAsync,
            decisions.ApplyAsync);
    }

    static async Task CatchUpAsync(IDomainEventReader events, Uuid tenantId, string projector,
        string area, ProjectionCheckpoint checkpoint,
        Func<ProjectionBatchContext, CancellationToken, ValueTask<IProjectionBatch>> begin,
        Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), area);
        await using var batch = await begin(new ProjectionBatchContext(
            new CheckpointIdentity(projector, pattern), checkpoint), CancellationToken.None);
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor,
                           CancellationToken.None))
        {
            await apply(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor), CancellationToken.None);
    }
}
