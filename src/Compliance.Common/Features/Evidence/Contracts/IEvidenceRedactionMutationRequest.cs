using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Evidence.Contracts;

public interface IEvidenceRedactionMutationRequest : IClientManagementMutationRequest
{
    Uuid RedactionId { get; }
}
