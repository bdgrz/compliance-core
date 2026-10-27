using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The facts of one acceptance or staff assignment on its engagement owner, in the order they were appended. The
///     operation pins the engagement content from its root admission until it records or rejects.
/// </summary>
public sealed class EngagementAcceptanceRecord(DomainEvent requested, EngagementIndependenceOperationKind kind)
{
    /// <summary>The root admission, which freezes the operation intent and pins the engagement content.</summary>
    public DomainEvent Requested { get; } = requested;

    /// <summary>Whether this is an acceptance or a positive staff assignment.</summary>
    public EngagementIndependenceOperationKind Kind { get; } = kind;

    /// <summary>The frozen intent of the operation.</summary>
    public ImmutableContentRef Intent { get; init; } = default!;

    /// <summary>The irreversible authorization, once appended.</summary>
    public EngagementPracticeCommitmentsAuthorized? Authorized { get; internal set; }

    /// <summary>The pre-authorization rejection, once appended.</summary>
    public DomainEvent? Rejected { get; internal set; }

    /// <summary>The recorded acceptance or assignment, once appended.</summary>
    public DomainEvent? Completed { get; internal set; }

    /// <summary>The terminal fact: the completion, or the rejection.</summary>
    public DomainEvent? Terminal => Completed ?? Rejected;

    /// <summary>True while the operation still pins the engagement content.</summary>
    public bool IsPending => Terminal is null;

    /// <summary>The root admission as a native operation fact.</summary>
    public INativeOperationFact RequestedOperation => (INativeOperationFact)Requested;
}
