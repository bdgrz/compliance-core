using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Current scalar engagement authority; terms, contacts, sources and history remain immutable content.</summary>
public sealed record ExaminationEngagementHeader(Uuid ProgramId, string EngagementType, Uuid BoundaryId,
    string Status, long Revision, Uuid? BaselineId, long BaselineRevision);
