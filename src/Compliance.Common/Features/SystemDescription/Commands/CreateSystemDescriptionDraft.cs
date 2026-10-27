using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.create-system-description-draft", 2)]
public sealed record CreateSystemDescriptionDraft(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ExaminationId, string Title, IReadOnlyList<SystemDescriptionSection> Sections) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => DescriptionId;
}
