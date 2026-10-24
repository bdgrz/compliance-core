using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>Publish point for one content id; written in the same transaction as, and after, its chunks.</summary>
sealed record ImmutableContentManifest(Uuid ContentId, string Sha256, long Bytes, string MediaType, string Schema,
    int ChunkCount, int ChunkBytes);
