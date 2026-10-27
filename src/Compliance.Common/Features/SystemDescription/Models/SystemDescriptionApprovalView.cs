using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>An approval of one exact version and its examination snapshot.</summary>
public sealed record SystemDescriptionApprovalView(Uuid VersionId, long Version,
    Uuid ApproverMemberId, string ApproverDisplay, string Rationale, Uuid? CloseSnapshotId,
    DateTimeOffset ApprovedAt);
