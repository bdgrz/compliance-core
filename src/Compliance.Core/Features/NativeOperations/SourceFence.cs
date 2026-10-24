using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
///     The extent of the committed facts a composed read folded: how many facts of its scope it read and the id of
///     the last one. Two reads of unchanged scope return equal fences, so a caller can prove a snapshot did not
///     change between two reads.
/// </summary>
public sealed record SourceFence(long Facts, Uuid? LastEventId);
