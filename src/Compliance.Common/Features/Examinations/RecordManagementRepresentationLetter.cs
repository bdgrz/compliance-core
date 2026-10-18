using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

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
