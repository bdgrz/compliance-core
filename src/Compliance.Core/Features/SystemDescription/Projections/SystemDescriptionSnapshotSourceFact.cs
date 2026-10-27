using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Projections;

public sealed record SystemDescriptionSnapshotSourceFact(NativeFactReference Fact, string EventKind, string PayloadSha256);
