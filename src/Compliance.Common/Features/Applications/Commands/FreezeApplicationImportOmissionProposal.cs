using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Personal HTTP confirmation of a source-bound omission preview. This is preparer intent, never retirement approval.</summary>
[Discriminator("bdgrz.application_import.omission.freeze", 2)]
public sealed record FreezeApplicationImportOmissionProposal(Uuid CommandId, Uuid TenantId, Uuid BatchId,
    long ExpectedRevision, ulong ExpectedSourcePosition, string ExpectedContentSha256, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
