using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.ratification-recorded", 2)]
public sealed record IndependenceRuleRatificationRecorded(IndependenceRuleRatificationHeader Header, ImmutableContentRef RatificationContent,
    long NativeSequence, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact
{
    public Uuid RequestId => Operation.LogicalId;
    public long ExpectedSequence => checked((long)Operation.OriginalExpectedRevision!.Value);
}
