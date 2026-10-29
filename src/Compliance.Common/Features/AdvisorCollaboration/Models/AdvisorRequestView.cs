using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>One advisor handoff with its immutable selection and attributable lifecycle state.</summary>
public sealed record AdvisorRequestView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorRequestId,
    IReadOnlyList<AdvisorRecordVersion> RecordVersions, string Purpose, DateOnly ExpiresOn,
    string Status, long Revision, ActorReference CreatedBy, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, Uuid? ComparedToHandoffId = null);
