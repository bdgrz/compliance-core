using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.record-system-description-no-change-decision", 2)]
public sealed record RecordSystemDescriptionNoChangeDecision(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, Uuid ChangeId, string Rationale, long ExpectedRevision) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
