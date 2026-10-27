using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Bounded current state. Prior source versions, responses, and links remain in the event stream.</summary>
public sealed record AuditObservationStateView(Uuid TenantId, Uuid ProgramId,
    Uuid AuditObservationId, long Revision, AuditObservationReceivedVersion Received,
    AuditObservationManagementResponseView? LatestManagementResponse,
    AuditObservationFindingLinkView? LatestFindingLink, DateTimeOffset UpdatedAt);
