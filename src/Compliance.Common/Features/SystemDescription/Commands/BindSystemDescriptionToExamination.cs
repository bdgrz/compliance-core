using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.SystemDescription.Commands;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.bind-system-description-to-examination", 2)]
public sealed record BindSystemDescriptionToExamination(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, Uuid ExaminationId, string BindingPurpose, long ExpectedRevision) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
