using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete frozen inputs of one historical client independence preview. Admission captures the exact service
///     epoch with every service of that epoch, the exact rule version content and its digest; the child preview reads
///     only this snapshot. <paramref name="HistoricalPracticeFacts" /> locates the permanent practice facts the
///     preview relied on and <paramref name="SourceCuts" /> records the committed frontier of each source owner read.
/// </summary>
/// <remarks>
///     Services are stored as complete <see cref="NonattestServiceView" /> items, a superset of the policy records, so
///     the preview can retain the original attribution. Until the fixed-cut reader exists a cut records the committed
///     stream position of its source owner, which the hydrated owner proves, rather than a global watermark.
/// </remarks>
public sealed record ClientIndependencePreviewIntent(DurableIntentHeader Header, Uuid EvaluationId,
    Uuid ClientTenantId, ulong ServiceEpoch, long RuleSetVersion, DateOnly ExaminationPeriodStart,
    ImmutableSelectionManifest CompleteServices, ImmutableContentRef RuleContent,
    ImmutableSelectionManifest HistoricalPracticeFacts, ImmutableSelectionManifest SourceCuts,
    string RuleVersionDigest)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "client-independence-preview-intent.v1";

    /// <summary>Schema of the rule version content.</summary>
    public const string RuleSchema = "independence-rule-version-view.v1";

    /// <summary>Item schema of the complete service selection.</summary>
    public const string ServiceItemSchema = "nonattest-service-view.v1";

    /// <summary>Item schema of the historical practice fact selection.</summary>
    public const string PracticeItemSchema = "source-fact-locator.v1";

    /// <summary>Item schema of the source cut selection.</summary>
    public const string CutItemSchema = "authoritative-read-cut.v1";
}
