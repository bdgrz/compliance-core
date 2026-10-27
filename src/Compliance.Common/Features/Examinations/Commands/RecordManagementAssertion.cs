using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records management wording and signer candidates against exact frozen examination inputs.</summary>
[Discriminator("bdgrz.examinations.record-management-assertion", 2)]
public sealed record RecordManagementAssertion(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationId,
    Uuid AssertionId,
    ManagementAssertionContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
