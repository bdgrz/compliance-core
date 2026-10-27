using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the sign management representation letter intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.sign-management-representation-letter", 1)]
public sealed record SignManagementRepresentationLetter(Uuid TenantId, Uuid ProgramId, Uuid LetterId, Uuid SignerMemberId, Uuid SignedArtifactId, DateTimeOffset SignedAt) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
