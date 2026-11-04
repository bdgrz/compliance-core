using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>One separate draft revision fixes its original methodology entry snapshot and author.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-draft-revised", 4)]
public sealed record MethodologyTemplateDraftRevisedV4(Uuid TemplateId, Uuid DraftId, long Revision,
    ImmutableContentRef EntriesContent, IReadOnlyList<string> EntryKeys, Uuid RevisedBy) : DomainEvent;
