using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds training requirement versions as the root native facts of their owner, without the request guards.</summary>
static class TrainingSeed
{
    public static async Task<Uuid> DefineAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        string identifier, TrainingRequirementContent content, ActorReference actor, DateTimeOffset at)
    {
        var normalized = TrainingRequirementIdentity.NormalizeIdentifier(identifier);
        var id = TrainingRequirementIdentity.IdFor(tenantId, programId, normalized);
        var result = await AppendAsync(provider, tenantId, id, "DefineTrainingRequirement", null, content, actor, at,
            (requirement, provenance, contentRef, hash, first, result) => requirement.Define(provenance, programId,
                normalized, contentRef, hash, actor, at, first, result), new TrainingRequirementRegistration(id,
                normalized, 1), id);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return id;
    }

    public static async Task ReviseAsync(IServiceProvider provider, Uuid tenantId, Uuid requirementId,
        string identifier, long expectedVersion, TrainingRequirementContent content, ActorReference actor,
        DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        var result = await AppendAsync(provider, tenantId, requirementId, "ReviseTrainingRequirement", expectedVersion,
            content, actor, at, (requirement, provenance, contentRef, hash, first, result) =>
                requirement.Revise(provenance, contentRef, hash, actor, at, first, result),
            new TrainingRequirementRegistration(requirementId, TrainingRequirementIdentity.NormalizeIdentifier(identifier),
                expectedVersion + 1), commandId);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    static async Task<Result> AppendAsync(IServiceProvider provider, Uuid tenantId, Uuid requirementId, string kind,
        long? expectedVersion, TrainingRequirementContent content, ActorReference actor, DateTimeOffset at,
        Func<TrainingRequirement, OperationProvenance, ImmutableContentRef, string, ImmutableContentRef,
            ImmutableContentRef, Result> apply, TrainingRequirementRegistration registration, Uuid logicalId)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var clean = TrainingRequirementRules.Clean(content);
        var contentRef = await TrainingRequirementRules.WriteContentAsync(store, tenantId, clean, CancellationToken.None);
        var body = await PolicyCampaignContent.WriteAsync(store, tenantId,
            PolicyCampaignContent.TrainingRegistrationSchema, registration, CancellationToken.None);
        var result = await PolicyCampaignContent.WriteResultAsync(store, tenantId,
            PolicyCampaignContent.TrainingRegistrationSchema, body, CancellationToken.None);
        var response = await FirstResponses.FreezeAsync(store, tenantId.ToString(),
            PolicyCampaignContent.OkResponse(PolicyCampaignContent.TrainingRegistrationSchema, body),
            CancellationToken.None);
        var stream = new TrainingRequirement(tenantId, requirementId).Stream;
        var provenance = PolicyCampaignContent.Provenance(logicalId, kind, stream, stream, actor, expectedVersion,
            new string('2', 64), at);
        var hash = TrainingRequirementRules.Hash(clean);
        return await executor.ExecuteAsync(new TrainingRequirement(tenantId, requirementId),
            requirement => AggregateOutcome.Commit(apply(requirement, provenance, contentRef, hash, response, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }
}
