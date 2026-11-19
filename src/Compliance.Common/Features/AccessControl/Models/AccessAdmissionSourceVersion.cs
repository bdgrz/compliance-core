namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>An observed source stream and physical position.</summary>
/// <param name="Stream">The canonical source stream address.</param>
/// <param name="Generation">Format 1 carries a non-reused source generation. Format 2 carries
/// <c>first-event:</c> followed by the actual first committed event ID; it does not claim a broker reset epoch.</param>
/// <param name="Position">The next committed physical position at the certified read.</param>
public sealed record AccessAdmissionSourceVersion(string Stream, string Generation, ulong Position);
