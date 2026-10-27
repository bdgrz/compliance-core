using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Public logical resource revision token, not a metadata version or append position.</summary>
public sealed record EntityConcurrency(string ResourceType, Uuid ResourceId, ulong Revision, string Episode);
