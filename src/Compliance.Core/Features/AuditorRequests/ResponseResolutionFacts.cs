using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     What a resolution reads from one response: the exact delivered version, the outcome of its external disposition
///     and the disposition fact, or the permanent reason the response cannot be held. These are the required facts of the
///     resolution use and are verified content.
/// </summary>
public sealed record ResponseResolutionFacts(Uuid ResponseId, int DeliveredVersion, string Outcome,
    NativeFactReference? DispositionFact, string? Refusal);
