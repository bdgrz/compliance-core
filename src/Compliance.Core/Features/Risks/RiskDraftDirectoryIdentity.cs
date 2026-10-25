using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftDirectoryIdentity(RiskDraftAuthorityHeader Header, ImmutableContentRef Content,
    long NativeSequence, NativeFactReference Source);
