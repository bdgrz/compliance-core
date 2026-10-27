namespace Bdgrz.Compliance.Features.Commitments.Models;

/// <summary>A context that cannot reference commitments, with the reason it contributes no dependents.</summary>
public sealed record CommitmentUnlinkedContext(string Context, string Reason);
