namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentChange(string Field, string? Before, string? After);
