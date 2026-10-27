using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Exact immutable close mutation binding the first owner, original command, prior authority and resulting body.</summary>
public sealed record TypeIIPeriodMutationContent(DurableIntentHeader Header, string Kind,
    string? BeforeSha256, ImmutableContentRef After, ImmutableContentRef? SourceProof = null)
{
    public const string Schema = "type-ii-period-mutation.v1";
}
