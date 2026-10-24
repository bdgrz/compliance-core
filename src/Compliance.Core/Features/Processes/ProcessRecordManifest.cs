namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Atomic publish point for the verified chunks of one mutable process record.</summary>
sealed record ProcessRecordManifest(int Bytes, int Chunks, string Sha256);
