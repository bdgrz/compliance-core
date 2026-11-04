using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Events;

/// <summary>The open separate draft's approved immutable revision became the next published version.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-version-published", 4)]
public sealed record MethodologyTemplateVersionPublishedV4(Uuid TemplateId, long Version, Uuid DraftId,
    long DraftRevision, Uuid ApprovalEventId, ImmutableContentRef EntriesContent, IReadOnlyList<string> EntryKeys,
    IReadOnlyList<string> ChangedEntryKeys, Uuid ApprovedBy) : DomainEvent;
