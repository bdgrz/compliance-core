using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Records a finding from a named source with its wording preserved. <paramref name="FindingId" /> is the logical
///     operation identity, so an exact retry replays the original registration.
/// </summary>
[Discriminator("bdgrz.finding.raise", 2)]
public sealed record RaiseFinding(Uuid FindingId, Uuid TenantId, Uuid ProgramId, FindingSource Source,
    string Title, string Description, string Severity, string AffectedScope,
    Uuid OwnerMemberId, DateOnly DueOn, IReadOnlyList<FindingLink>? Links = null)
    : IRequest<FindingRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => FindingId;
}
