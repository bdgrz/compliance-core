using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>Exact content proposed for a draft advisor handoff and its baseline comparison.</summary>
public sealed record AdvisorHandoffPreviewView(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid? ComparedToHandoffId, long Revision, string Purpose, DateOnly ExpiresOn,
    IReadOnlyList<AdvisorHandoffPreviewItemView> SelectedSources,
    IReadOnlyList<AdvisorRecordVersion> RemovedSources);
