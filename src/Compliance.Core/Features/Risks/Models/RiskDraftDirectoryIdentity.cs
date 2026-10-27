using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskDraftDirectoryIdentity(RiskDraftAuthorityHeader Header, ImmutableContentRef Content,
    long NativeSequence, NativeFactReference Source);
