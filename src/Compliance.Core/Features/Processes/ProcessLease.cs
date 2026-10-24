namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Worker lease on a record. It only steers which worker resumes; expiry never releases authority.</summary>
public sealed record ProcessLease(string Worker, DateTimeOffset Until);
