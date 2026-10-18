using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>An immutable link between an approved version and its exact examination snapshot.</summary>
public sealed record SystemDescriptionBindingView(Uuid VersionId, long Version,
    Uuid ExaminationId, string Purpose, Uuid SnapshotId, Uuid BoundByMemberId,
    string BoundByDisplay, DateTimeOffset BoundAt);
