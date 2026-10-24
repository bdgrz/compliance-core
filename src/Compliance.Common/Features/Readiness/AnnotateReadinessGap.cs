using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Records attributed advisor feedback on a gap of the latest assessment; HTTP-only.
///     <paramref name="ExpectedRevision" /> is the revision of the gap plan, which an annotation never advances.
/// </summary>
[Discriminator("bdgrz.readiness.gap.annotate", 2)]
public sealed record AnnotateReadinessGap(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    Uuid GapId, long ExpectedRevision, string Body)
    : IRequest<ReadinessAnnotationView>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
