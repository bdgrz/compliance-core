using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record SnapshotRegistration(Uuid SnapshotId, string ContentSha256);
