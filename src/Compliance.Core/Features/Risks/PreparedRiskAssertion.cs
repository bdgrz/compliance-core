using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Risks;

sealed record PreparedRiskAssertion(FirstResponseDescriptor Response, ImmutableContentRef Result,
    ImmutableContentRef? Proposal = null);
