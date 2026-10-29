using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Arranges committed evidence-request owner history for consumer tests; command tests use the handlers.</summary>
static class EvidenceSeeds
{
    public static async Task OpenAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid requestId,
        string title, string instructions, Uuid owner, DateOnly due, Uuid? control, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var request = new EvidenceRequest(tenant, program, requestId);
        var view = new EvidenceRequestView(tenant, program, requestId, 1, title, instructions, owner, due, control,
            EvidenceRequest.Open, actor, at, null, null, null, null, null, null);
        var (response, result) = await FindingContent.FreezeAsync(content, tenant, EvidenceRequestRules.ViewSchema, view,
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = RiskSeeds.Operation(request, requestId, 0, actor, at, nameof(OpenEvidenceRequest), view);
        await ProgramManagementServices.SeedAsync(provider, request, source => source.OpenRequest(operation,
            new EvidenceRequestTerms(title, instructions, owner, due, control, actor, at), first, result));
    }

    public static async Task FulfilAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid requestId,
        Uuid artifactId, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var request = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EvidenceRequest(tenant, program, requestId));
        var view = request.View()! with
        {
            Revision = request.Revision + 1,
            Status = EvidenceRequest.Fulfilled,
            ArtifactId = artifactId,
            FulfilledBy = actor,
            FulfilledAt = at
        };
        var (response, result) = await FindingContent.FreezeAsync(content, tenant, EvidenceRequestRules.ViewSchema, view,
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = RiskSeeds.Operation(request, Uuid.CreateVersion4(), (ulong)request.Revision, actor, at,
            nameof(FulfilEvidenceRequest), view);
        await ProgramManagementServices.SeedAsync(provider, new EvidenceRequest(tenant, program, requestId),
            source => source.Fulfil(operation, artifactId, actor, at, first, result));
    }

    public static async Task CancelAsync(IServiceProvider provider, Uuid tenant, Uuid program, Uuid requestId,
        string rationale, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var request = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EvidenceRequest(tenant, program, requestId));
        var view = request.View()! with
        {
            Revision = request.Revision + 1,
            Status = EvidenceRequest.Cancelled,
            CancellationRationale = rationale,
            CancelledBy = actor,
            CancelledAt = at
        };
        var (response, result) = await FindingContent.FreezeAsync(content, tenant, EvidenceRequestRules.ViewSchema, view,
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenant.ToString(), response);
        var operation = RiskSeeds.Operation(request, Uuid.CreateVersion4(), (ulong)request.Revision, actor, at,
            nameof(CancelEvidenceRequest), view);
        await ProgramManagementServices.SeedAsync(provider, new EvidenceRequest(tenant, program, requestId),
            source => source.Cancel(operation, rationale, actor, at, first, result));
    }
}
