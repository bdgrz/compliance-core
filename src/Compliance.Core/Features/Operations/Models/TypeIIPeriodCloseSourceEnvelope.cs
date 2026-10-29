using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Exact effective interval and receipt cutoff sealed with complete native source history.</summary>
public sealed record TypeIIPeriodCloseSourceEnvelope(DurableIntentHeader Header, DateOnly EffectiveFrom,
    DateOnly EffectiveThrough, DateTimeOffset RecordedThrough, string Family,
    IReadOnlyList<TypeIIPeriodCloseNativeSource> Records,
    IReadOnlyList<TypeIIPeriodCloseOccurrenceMatch> OccurrenceMatches);
