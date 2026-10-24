using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One occurrence a generation selected: the period's expected occurrence it satisfies, the canonical
///     occurrence identity, and the reference to its frozen <see cref="ControlOccurrenceCreationIntent" />.
/// </summary>
public sealed record ControlOccurrenceSelection(Uuid ExpectedOccurrenceId, Uuid OccurrenceId,
    ImmutableContentRef Intent);
