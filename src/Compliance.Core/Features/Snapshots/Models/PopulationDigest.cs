namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record PopulationDigest(long RowCount, int ChunkCount, string Sha256);
