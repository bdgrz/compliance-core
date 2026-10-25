using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

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
