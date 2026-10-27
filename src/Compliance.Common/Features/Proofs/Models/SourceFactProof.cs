using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Proofs.Models;

/// <summary>Server-created proof that a concrete native event and its exact required facts were verified.</summary>
public abstract record SourceFactProof(EventStreamAddress Source, Uuid EventId, ulong PhysicalPosition,
    ulong OwnerRevision, string EventKind, string PayloadSha256, ImmutableContentRef RequiredFacts,
    ulong GlobalPosition, ulong RealmPosition);
