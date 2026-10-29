using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

sealed record PreparedRiskAssertion(FirstResponseDescriptor Response, ImmutableContentRef Result,
    ImmutableContentRef? Proposal = null);
