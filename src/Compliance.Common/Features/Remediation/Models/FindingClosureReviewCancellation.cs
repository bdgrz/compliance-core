using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The exact accepted review fenced by its proposal's withdrawal, including every source use to settle.</summary>
public sealed record FindingClosureReviewCancellation(NativeFactReference Admission, ImmutableContentRef Intent,
    Uuid RootOperationId, long Generation);
