using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     The reviewer's personal, independent decision on the latest attestation; HTTP-only. The
///     performer or recorder cannot review it without an approved waiver. <paramref name="CommandId" /> is the
///     logical operation identity and the review decision identity.
/// </summary>
[Discriminator("bdgrz.control.occurrence.review", 2)]
public sealed record ReviewControlOccurrence(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, Uuid AttestationId, string Outcome, string Rationale,
    IReadOnlyList<string>? RequestedActions = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
