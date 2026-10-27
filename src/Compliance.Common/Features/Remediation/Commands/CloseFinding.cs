using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The reviewer's personal verification and closure decision; HTTP-only. It seals action admission, freezes the
///     complete admitted action manifest and is acknowledged with 202; <c>FindingClosureProcess</c> then holds every
///     completed action, verifies independence (an exact waiver proof replaces a live waiver) and appends the closure.
/// </summary>
[Discriminator("bdgrz.finding.close", 2)]
public sealed record CloseFinding(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string VerificationRationale,
    IReadOnlyList<EvidenceReference> ResolutionEvidence, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
