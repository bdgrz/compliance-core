using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>
///     The frozen terms of one admitted child: the parent and its generation at admission, the complete canonical
///     authored child content and the parent revision the admission was accepted at. The materializing receiver and
///     its owner never load the live parent.
/// </summary>
public sealed record ChildAdmissionIntent(DurableIntentHeader Header, Uuid ParentId, ulong ParentGeneration,
    AdmittedChildKind ChildKind, Uuid ChildId, ImmutableContentRef CanonicalAuthoredContent,
    ImmutableSelectionManifest SelectedSourceProofs, ulong ParentAdmissionRevision)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "child-admission-intent.v1";
}
