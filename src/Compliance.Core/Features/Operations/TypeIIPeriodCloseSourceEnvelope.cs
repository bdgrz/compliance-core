using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Exact effective interval and receipt cutoff sealed with complete native source history.</summary>
public sealed record TypeIIPeriodCloseSourceEnvelope(DurableIntentHeader Header, DateOnly EffectiveFrom,
    DateOnly EffectiveThrough, DateTimeOffset RecordedThrough, string Family,
    IReadOnlyList<TypeIIPeriodCloseNativeSource> Records,
    IReadOnlyList<TypeIIPeriodCloseOccurrenceMatch> OccurrenceMatches);
