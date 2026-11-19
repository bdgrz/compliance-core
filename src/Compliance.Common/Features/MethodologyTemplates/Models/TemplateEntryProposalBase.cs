using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>The immutable approved destination revision selected for one methodology proposal.</summary>
public sealed record TemplateEntryProposalBase(string EntryKey, string RecordKind, Uuid RecordId,
    Uuid? ApprovedVersionId, long? ApprovedRevision);
