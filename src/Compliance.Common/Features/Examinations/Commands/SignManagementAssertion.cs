using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Signs the current assertion using an exact validated package and the examination's named signer authority.</summary>
[Discriminator("bdgrz.examinations.sign-management-assertion", 1)]
public sealed record SignManagementAssertion(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    Uuid AssertionId, long ExpectedRevision, int AssertionVersion, Uuid PackageId, int PackageVersion)
    : IRequest, IProgramReadRequest, IClientManagementMutationRequest, ICallable;
