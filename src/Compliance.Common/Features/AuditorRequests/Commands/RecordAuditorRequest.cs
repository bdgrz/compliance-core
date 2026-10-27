using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Records one auditor request. <paramref name="RequestId" /> is the logical operation and the request identity, so
///     an exact retry replays the original Unit result and a changed retry conflicts.
/// </summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-request", 2)]
public sealed record RecordAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ExaminationId,
    string Source, string ExternalReference, string Wording, string Scope, string Priority, Uuid OwnerMemberId,
    DateOnly DueOn)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable;
