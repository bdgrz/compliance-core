using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds evaluation streams directly on their owner, as a prior accepted operation.</summary>
static class EvaluationSeeds
{
    /// <summary>Starts an evaluation with the frozen procedure written as immutable content.</summary>
    public static async Task StartAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid controlVersionId, Uuid planVersionId, long planVersion,
        IReadOnlyList<EvaluationProcedureStep> steps, Uuid evaluatorMemberId, string display, DateTimeOffset at,
        Uuid? retestOfEvaluationId = null, NativeFactReference? retestPredecessor = null,
        IReadOnlyList<Uuid>? retestDeviations = null)
    {
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var manifest = await NativeContent.WriteAsync(content, tenantId, ControlEvaluationContent.StepsSchema, steps);
        var retested = await NativeContent.WriteAsync(content, tenantId, ControlEvaluationContent.RetestDeviationsSchema,
            retestDeviations ?? []);
        await ProgramManagementServices.SeedAsync(provider, new ControlEvaluation(tenantId, evaluationId, evaluationId),
            owner => owner.Start(NativeFacts.Root(owner, evaluationId, "StartControlEvaluation", null, evaluatorMemberId,
                    at) with
            { Actor = ActorReference.ForMember(evaluatorMemberId, display) }, programId, controlId, controlVersionId, planVersionId, planVersion, manifest, evaluatorMemberId,
                ActorReference.ForMember(evaluatorMemberId, display), at, retestOfEvaluationId, retestPredecessor,
                retested, NativeFacts.Ref(), NativeFacts.Ref()));
    }
}
