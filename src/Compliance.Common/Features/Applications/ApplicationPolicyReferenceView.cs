using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A bounded policy relationship and lifecycle fact affecting an application system.</summary>
public sealed record ApplicationPolicyReferenceView(Uuid TenantId, string SubjectType,
    Uuid GovernedRecordId, Uuid ProgramId, Uuid PolicyId, string Identifier, string Title,
    long? Version, long Revision, string Status, DateOnly? EffectiveFrom,
    DateOnly? EffectiveUntil, Uuid EntryId, string Subject, bool HasPendingSuccessor,
    DateOnly? PendingRetirementEffectiveUntil);
