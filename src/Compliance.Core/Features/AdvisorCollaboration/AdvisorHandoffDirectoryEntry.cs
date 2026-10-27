using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

sealed record AdvisorHandoffDirectoryEntry(Uuid TenantId, Uuid ProgramId,
    Uuid AdvisorRequestId, string Name, string Status, DateTimeOffset UpdatedAt,
    DateOnly ExpiresOn, long Revision, IReadOnlyList<AdvisorRecordVersion> Selection,
    IReadOnlyList<Uuid> FeedbackIds, Uuid LastEventId, ulong LastAggregateVersion,
    Uuid? ComparedToHandoffId = null);
