using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Contracts;

/// <summary>A named Program mutation requiring a current client Compliance Lead.</summary>
public interface IProgramLeadMutationRequest : IRequestBase
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
}
