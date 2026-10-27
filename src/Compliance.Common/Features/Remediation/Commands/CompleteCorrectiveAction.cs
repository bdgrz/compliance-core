using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     The action owner, or a program manager, records the completed corrective work on the action's own owner.
///     <paramref name="ExpectedRevision" /> is the revision of the corrective action, not of the finding.
/// </summary>
[Discriminator("bdgrz.finding.corrective_action.complete", 2)]
public sealed record CompleteCorrectiveAction(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ActionId, string ResolutionNotes,
    IReadOnlyList<EvidenceReference> Evidence)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
