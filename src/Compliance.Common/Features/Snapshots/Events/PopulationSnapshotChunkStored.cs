using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Events;

/// <summary>
///     One ordered storage chunk of a population snapshot's rows. Chunks are written before the
///     snapshot's frozen manifest, so an interrupted freeze leaves no visible snapshot.
///     <paramref name="ChunkSha256" /> is the population digest of this chunk's rows alone.
/// </summary>
[Discriminator("bdgrz.snapshot.population.chunk_stored", 1)]
public sealed record PopulationSnapshotChunkStored(Uuid TenantId, Uuid SnapshotId, int ChunkIndex,
    string Kind, IReadOnlyList<PopulationRow> Rows, string ChunkSha256) : DomainEvent;
