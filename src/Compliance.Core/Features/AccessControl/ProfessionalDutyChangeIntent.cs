using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The frozen terms of one professional-duty change: the exact authored command, the signed assertion and its
///     verified scope, the predecessor fact and, for recording, the exact staff use the change needs. Every receiver
///     copies it; the signed validity interval is judged at <c>Header.AcceptedAt</c>, the declared decision instant.
/// </summary>
/// <remarks>
///     <paramref name="PreviousAssertion" /> is the native reference of the designation fact that was current at
///     admission (a locator without broker positions until the stream-backed resolver is registrable).
/// </remarks>
public sealed record ProfessionalDutyChangeIntent(DurableIntentHeader Header, Uuid DesignationId,
    ProfessionalDutyChangeKind ChangeKind, ImmutableContentRef AuthoredChange, string SignedAssertion,
    Uuid VerifiedAssertionId, ulong AuthorityRevision, ImmutableContentRef VerifiedScope,
    NativeFactReference? PreviousAssertion, SourceUseRequirement? StaffEligibility)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "professional-duty-change-intent.v1";
}
