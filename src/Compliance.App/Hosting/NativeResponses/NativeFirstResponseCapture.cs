using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>One request-local original response; ambiguity fails closed instead of choosing a later result.</summary>
public sealed record NativeFirstResponseCapture(AcceptedOperationBinding Binding, NativeFactReference Fact,
    ImmutableContentRef FirstResponse, FirstResponseDescriptor Response, bool Ambiguous = false);
