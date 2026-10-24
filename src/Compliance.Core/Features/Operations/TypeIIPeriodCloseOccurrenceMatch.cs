using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>An exact approved expected slot and the native occurrence/creation terms satisfying it.</summary>
public sealed record TypeIIPeriodCloseOccurrenceMatch(Uuid ExpectedOccurrenceId, NativeFactReference Opened,
    ImmutableContentRef CreationIntent);
