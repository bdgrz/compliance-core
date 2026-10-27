using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Replay-safe intent and receipt for one owning-workflow draft creation.</summary>
public sealed record TemplateApplicationDraftView(
    string EntryKey,
    string RecordKind,
    Uuid PlannedRecordId,
    string Status,
    Uuid? CreatedRecordId,
    long? CreatedRecordVersion,
    string? CreatedRecordSha256);
