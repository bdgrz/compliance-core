using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>One normalized global template name was admitted for a line and frozen platform authoring intent.</summary>
[Discriminator("bdgrz.methodology-template-names.methodology-template-name-claimed", 1)]
public sealed record MethodologyTemplateNameClaimed(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, OperationProvenance Operation, ImmutableContentRef FirstResponse) : DomainEvent, INamedRecordFact
{
    ImmutableContentRef? INamedRecordFact.SourceProofs => null;
}
