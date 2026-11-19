using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>Committed conflicting line creation refused this intent; only a still-held losing name generation was released.</summary>
[Discriminator("bdgrz.methodology-template-names.methodology-template-creation-refused", 1)]
public sealed record MethodologyTemplateCreationRefused(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef SourceProofs, OperationProvenance Operation,
    Uuid ClaimEventId, Uuid ExistingCreationEventId, bool ReleasedName, string Reason) : DomainEvent, INamedRecordFact
{
    ImmutableContentRef? Bdgrz.Compliance.Features.NativeOperations.Contracts.INativeOperationFact.FirstResponse => null;
}
