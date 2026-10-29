using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records an exact auditor-source letter against an approved assertion and delivered package.</summary>
[Discriminator("bdgrz.examinations.record-management-representation-letter", 2)]
public sealed record RecordManagementRepresentationLetter(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationId,
    Uuid LetterId,
    Uuid AssertionId,
    Uuid ArtifactId,
    string SourceReference,
    DateOnly LetterDate,
    IReadOnlyList<Uuid> RequiredSignerIds)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
