using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Bounded current state. Prior source versions, responses, and links remain in the event stream.</summary>
public sealed record AuditObservationStateView(Uuid TenantId, Uuid ProgramId,
    Uuid AuditObservationId, long Revision, AuditObservationReceivedVersion Received,
    AuditObservationManagementResponseView? LatestManagementResponse,
    AuditObservationFindingLinkView? LatestFindingLink, DateTimeOffset UpdatedAt);
