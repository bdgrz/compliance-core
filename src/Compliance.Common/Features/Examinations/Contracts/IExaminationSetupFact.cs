using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Contracts;

/// <summary>A named examination setup transition with its exact authored acceptance.</summary>
public interface IExaminationSetupFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    Uuid CommandId { get; }
    long OriginalExpectedRevision { get; }
    string AuthoredIntentSha256 { get; }
    Uuid ActorMemberId { get; }
    string ActorDisplay { get; }
    DateTimeOffset OccurredAt { get; }
    ExaminationView State { get; }
}
