using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

static class BoundaryDirectorySchema
{
    public static readonly KvDirectoryIndex<BoundaryDirectoryIdentity> VersionsByBoundary = new("by_boundary", 1,
        static value => [value.Header.BoundaryId.ToString(), value.EffectiveFrom!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);
    public static readonly KvDirectory<BoundaryDirectoryIdentity, string> Versions = new("boundary_version_identities",
        ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, static value => ScopedKey(value.Header.BoundaryId, value.VersionId!.Value),
        static key => [key], [VersionsByBoundary]);
    public static readonly KvDirectoryIndex<BoundaryDirectoryIdentity> DecisionsByBoundary = new("by_boundary", 1,
        static value => [value.Header.BoundaryId.ToString(), value.DecidedAt!.Value.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            value.DecisionId!.Value.ToString()]);
    public static readonly KvDirectory<BoundaryDirectoryIdentity, string> Decisions = new("boundary_decision_identities",
        ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, static value => ScopedKey(value.Header.BoundaryId, value.DecisionId!.Value),
        static key => [key], [DecisionsByBoundary]);
    public static readonly KvDirectoryIndex<BoundaryDirectoryIdentity> ByProgram = new("by_program", 1,
        static value => [value.Header.ProgramId.ToString(), value.Header.BoundaryId.ToString()]);
    public static readonly KvDirectory<BoundaryDirectoryIdentity, Uuid> Directory = new("boundary_current_identities",
        ComplianceCoreJsonContext.Default.BoundaryDirectoryIdentity, static value => value.Header.BoundaryId,
        static id => [id.ToString()], [ByProgram]);

    public static string ScopedKey(Uuid boundaryId, Uuid localId) => $"{boundaryId}:{localId}";
}
