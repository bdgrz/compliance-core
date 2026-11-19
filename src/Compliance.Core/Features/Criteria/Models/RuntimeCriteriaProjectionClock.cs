namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Snapshot identity advanced atomically with every projected row mutation and checkpoint.</summary>
sealed record RuntimeCriteriaProjectionClock(string Generation, long Ordinal,
    string Epoch, string SourceCursor, long DirectoryRevision);
