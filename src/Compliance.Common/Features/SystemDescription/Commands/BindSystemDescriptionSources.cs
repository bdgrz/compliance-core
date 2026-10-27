using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Commands;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.bind-system-description-sources", 2)]
public sealed record BindSystemDescriptionSources(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long ExpectedRevision, IReadOnlyList<Uuid> SourceVersionIds) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
