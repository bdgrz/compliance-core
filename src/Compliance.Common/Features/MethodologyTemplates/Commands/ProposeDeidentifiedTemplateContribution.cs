using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Proposes one de-identified source record for independent methodology review.</summary>
[Discriminator("bdgrz.methodology-templates.propose-deidentified-template-contribution", 2)]
public sealed record ProposeDeidentifiedTemplateContribution(Uuid TenantId, Uuid ContributionId,
    Uuid SourceRecordId, long SourceVersion, MethodologyTemplateRecordContent ProposedContent,
    string DeidentificationRationale) : IRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ContributionId;
}
