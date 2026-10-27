using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.revised", 2)]
public sealed record IndependenceRulesRevised(IndependenceRuleCatalogHeader Header, ImmutableContentRef RuleContent,
    long NativeSequence, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact
{
    public Uuid RequestId => Operation.LogicalId;
    public long ExpectedSequence => checked((long)Operation.OriginalExpectedRevision!.Value);
}
