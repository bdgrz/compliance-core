using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.generate-system-description-export", 2)]
public sealed record GenerateSystemDescriptionExport(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, string Format, string ExportKey) : IRequest, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
