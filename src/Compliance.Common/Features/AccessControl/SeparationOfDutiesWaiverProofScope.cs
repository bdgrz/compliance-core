using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Everything a waiver must match exactly before it authorizes an action; no field is optional.</summary>
public sealed record SeparationOfDutiesWaiverProofScope(
    Uuid TenantId,
    Uuid WaiverId,
    Uuid SubjectId,
    Uuid ActorId,
    string Action,
    string TargetType,
    Uuid TargetId,
    Uuid TargetVersionId,
    long TargetRevision);
