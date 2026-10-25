using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed record SystemDescriptionSnapshotSourceFact(NativeFactReference Fact, string EventKind, string PayloadSha256);
