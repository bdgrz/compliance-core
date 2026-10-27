using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records an attributable amendment to a frozen Type I baseline.</summary>
[Discriminator("bdgrz.examinations.amend-type-i-baseline", 1)]
public sealed record AmendTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    Uuid BaselineId, long ExpectedRevision, string Reason, IReadOnlyList<Uuid> ChangedRecordIds,
    string ExpectedPreviewSha256, IReadOnlyList<Uuid> AffectedPackageMaterialIds)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => BaselineId;
}
