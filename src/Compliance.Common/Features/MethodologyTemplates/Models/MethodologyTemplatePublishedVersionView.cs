using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Actual immutable published entries and recorded separate-draft approval, with their exact native source.</summary>
public sealed record MethodologyTemplatePublishedVersionView(Uuid TemplateId, Uuid VersionId, long Version,
    Uuid DraftId, long DraftRevision, Uuid ApprovalEventId, Uuid ApprovedBy, DateTimeOffset PublishedAt,
    ImmutableContentRef EntriesContent, IReadOnlyList<MethodologyTemplateRecordContent> Entries,
    IReadOnlyList<string> ChangedEntryKeys, NativeFactReference PublicationSource, ulong PublicationPosition);
