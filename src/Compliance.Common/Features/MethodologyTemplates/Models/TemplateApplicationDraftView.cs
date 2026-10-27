using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Replay-safe intent and receipt for one owning-workflow draft creation.</summary>
public sealed record TemplateApplicationDraftView(
    string EntryKey,
    string RecordKind,
    Uuid PlannedRecordId,
    string Status,
    Uuid? CreatedRecordId,
    long? CreatedRecordVersion,
    string? CreatedRecordSha256);
