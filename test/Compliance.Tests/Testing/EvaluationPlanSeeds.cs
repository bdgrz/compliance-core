using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
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
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Native source fixtures; explicit imported version identities remain test inputs, never a public callback.</summary>
static class EvaluationPlanSeeds
{
    public static async Task WriteAsync(IServiceProvider provider, ControlEvaluationPlanVersionView version)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var body = await NativeContent.WriteAsync(content, version.TenantId, ControlEvaluationPlanReader.VersionSchema, version);
        var frozen = await NativeContent.FreezeAsync(content, version.TenantId, ControlEvaluationPlanReader.VersionSchema, version);
        var first = await FirstResponses.FreezeAsync(content, version.TenantId.ToString(), frozen.Response);
        var owner = new ControlEvaluationPlan(version.TenantId, version.ProgramId, version.ControlId, version.PlanVersionId);
        var operation = RootOperation.Provenance(version.PlanVersionId, nameof(DefineControlEvaluationPlan), owner.Stream,
            version.Author, version.Version - 1, ContentDigest.OfJson(version), version.DefinedAt) with
        { FirstOwner = owner.Stream };
        var result = await services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            current => AggregateOutcome.Commit(current.Define(operation, version.ControlVersionId, body, first, frozen.Result)),
            new RequestDispatchContext(RequestActor.System));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
