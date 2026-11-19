using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>The exact committed readiness decision or examination outcome relied on by a stage decision.</summary>
public sealed record ProgramStageSupport(string Kind, Uuid ProgramId, SourceFactLocator Source,
    ImmutableContentRef Snapshot);
