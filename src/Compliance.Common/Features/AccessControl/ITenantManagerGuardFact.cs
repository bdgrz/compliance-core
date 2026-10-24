using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The shared shape of the facts the tenant manager guard appends for an administrator transition.</summary>
public interface ITenantManagerGuardFact : INativeOperationFact
{
    /// <summary>The source stream the operation mutates.</summary>
    EventStreamAddress Owner { get; }

    /// <summary>The authored revision of <see cref="Owner" />; negative when unconstrained.</summary>
    long Revision { get; }

    /// <summary>The guard generation of this fact.</summary>
    long Generation { get; }

    /// <summary>The frozen administrator transition intent.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The source proofs the fact pinned, such as the authoritative cut behind a first fact.</summary>
    ImmutableContentRef? SourceProofs { get; }
}
