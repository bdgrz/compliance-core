using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Reads and verifies concrete native source events by their exact broker position.</summary>
public interface ISourceFactResolver
{
    /// <summary>Reads the event at one resource position with its locator. Audit records are never resolved.</summary>
    /// <exception cref="SourceFactVerificationException">The record is missing, an audit record, or lacks offsets.</exception>
    Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
        CancellationToken ct = default);

    /// <summary>
    /// Re-reads the exact record named by the proof and returns a trusted proof only when stream, realm, event id,
    /// kind, positions, aggregate version and payload digest all match.
    /// </summary>
    /// <exception cref="SourceFactVerificationException">Any part of the proof does not match the stored record.</exception>
    Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
        CancellationToken ct = default) where TEvent : DomainEvent;
}
