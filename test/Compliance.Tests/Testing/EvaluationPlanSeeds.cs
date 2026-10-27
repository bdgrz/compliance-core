using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

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
