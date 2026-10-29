using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Providers.Models;

/// <summary>
///     The frozen admission of one accepted-risk link. <paramref name="LinkId" /> equals the root command id. The gap
///     serialized the open check, the exact expected revision and the uniqueness of the (risk, acceptance) pair;
///     <paramref name="GapAdmissionRevision" /> is the gap revision that admission produced.
///     <paramref name="LinkedAt" /> is the admission instant, when the source acceptance was proven active.
/// </summary>
public sealed record ProviderRiskLinkIntent(DurableIntentHeader Header, Uuid LinkId, Uuid ProviderId, Uuid GapId,
    ulong GapAdmissionRevision, Uuid ProgramId, Uuid RiskId, Uuid AcceptanceId,
    SourceFactProof<RiskAccepted> AcceptanceProof, DateTimeOffset AcceptanceExpiresAt, ulong ProviderRevision,
    Uuid? ServiceId, DateTimeOffset LinkedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "provider-risk-link-intent.v1";
}
