using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     The complete frozen plan of one import batch: every planned row in authored order (retirement rows after them
///     in their deterministic proposal order), every canonical target group and every source claim the plan was frozen
///     against, plus the actors, the source cut and the exact source revision. Nothing is inferred from projection
///     absence, and no manifest is truncated: each is immutable content of any size.
///     <paramref name="SourceRevision" /> is the source's logical revision the plan was frozen against. A
///     cancellation freezes the same shape under its own header: the sealed plan's manifests (empty when the batch was
///     never sealed) and its <paramref name="CancellationReason" />.
/// </summary>
public sealed record ImportFrozenPlanIntent(DurableIntentHeader Header, Uuid BatchId, string SourceKey,
    string SourceNamespace, string Coverage, string ContentSha256, Uuid SubmitterMemberId, Uuid ApproverMemberId,
    ImmutableSelectionManifest Rows, ImmutableSelectionManifest TargetGroups, ImmutableSelectionManifest SourceClaims,
    ImmutableContentRef? OmissionProposal, AuthoritativeReadCut SourceCut, string SubmitterDisplay,
    string ApproverDisplay, long SourceRevision, string? CancellationReason = null)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "import-frozen-plan-intent.v1";
}
