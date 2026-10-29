using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

public interface IAccessGrantProposalValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
        CancellationToken ct = default);
}
