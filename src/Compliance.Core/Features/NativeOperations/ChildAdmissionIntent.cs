using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
///     The frozen terms of one child result a parent owner admitted: the exact parent and its open generation, the
///     kind and identity of the child, the complete normalized authored request, the source proofs selected at
///     admission and the parent's logical revision after the admission. Every child effect copies the header and never
///     reads the live parent for what was authored.
/// </summary>
public sealed record ChildAdmissionIntent(DurableIntentHeader Header, Uuid ParentId, ulong ParentGeneration,
    AdmittedChildKind ChildKind, Uuid ChildId, ImmutableContentRef CanonicalAuthoredContent,
    ImmutableSelectionManifest SelectedSourceProofs, ulong ParentAdmissionRevision)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "child-admission-intent.v1";
}
