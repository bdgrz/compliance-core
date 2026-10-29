using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>
///     Appends an attributed correction to feedback on its own thread; the original contribution is preserved.
///     <paramref name="CorrectionId" /> is the logical operation identity and <paramref name="ExpectedRevision" /> is
///     the revision of the feedback thread.
/// </summary>
[Discriminator("bdgrz.advisor-collaboration.correct-advisor-feedback", 2)]
public sealed record CorrectAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, Uuid CorrectionId, string CorrectedBody, string CorrectionReason, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CorrectionId;
}
