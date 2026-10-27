using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Complete request state and retained response, discussion, and sample history.</summary>
public sealed record AuditorRequestView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditorRequestId,
    Uuid ExaminationId,
    string Source,
    string ExternalReference,
    string Wording,
    string Scope,
    string Priority,
    Uuid OwnerMemberId,
    DateOnly DueOn,
    bool RequiresInternalReview,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    IReadOnlyList<AuditorRequestTermsVersion> TermsHistory,
    IReadOnlyList<AuditorRequestCommentView> Comments,
    IReadOnlyList<AuditorRequestResponseHistoryView> Responses,
    IReadOnlyList<AuditorSampleSelectionView> SampleSelections,
    IReadOnlyList<AuditorRequestHistoryEntry> History);
