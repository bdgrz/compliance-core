using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>A bounded reference to an approved control version affecting an application system.</summary>
public sealed record ApplicationControlVersionReferenceView(Uuid TenantId, string SubjectType,
    Uuid GovernedRecordId, Uuid ProgramId, Uuid ControlId, string Identifier, Uuid VersionId,
    long Revision, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveUntil,
    Uuid EntryId, string Subject, string Rationale, bool HasPendingSuccessor,
    DateOnly? PendingRetirementEffectiveUntil);
