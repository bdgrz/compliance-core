namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Records kept inside the cut for one selector and realm, with the cursor that resumes the scan.</summary>
public sealed record SnapshotReadResult(IReadOnlyList<ResolvedSourceFact> Facts, SnapshotCursor Cursor,
    bool IsComplete);
