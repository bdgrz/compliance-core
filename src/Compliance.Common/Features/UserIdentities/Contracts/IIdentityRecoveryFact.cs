using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>The shared shape of the native facts the identity recovery owner appends.</summary>
public interface IIdentityRecoveryFact : INativeOperationFact
{
    /// <summary>The stream of the owner that appended the fact.</summary>
    EventStreamAddress Owner { get; }

    /// <summary>The revision of <see cref="Owner" /> the fact was appended at.</summary>
    long Revision { get; }

    /// <summary>The recovery generation; one challenge freezes exactly one generation.</summary>
    long Generation { get; }

    /// <summary>The frozen identity recovery intent.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The source proofs the fact pinned.</summary>
    ImmutableContentRef? SourceProofs { get; }

    /// <summary>The recovery challenge, which is the logical identity of the recovery owner.</summary>
    Uuid ChallengeId { get; }
}
