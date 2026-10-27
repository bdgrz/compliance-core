using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Proofs.Models;

/// <summary>Source fact proof for one native source event type.</summary>
/// <typeparam name="TSourceEvent">The native event type the proof was verified against.</typeparam>
public sealed record SourceFactProof<TSourceEvent>(EventStreamAddress Source, Uuid EventId, ulong PhysicalPosition,
    ulong OwnerRevision, string EventKind, string PayloadSha256, ImmutableContentRef RequiredFacts,
    ulong GlobalPosition, ulong RealmPosition)
    : SourceFactProof(Source, EventId, PhysicalPosition, OwnerRevision, EventKind, PayloadSha256, RequiredFacts,
        GlobalPosition, RealmPosition);
