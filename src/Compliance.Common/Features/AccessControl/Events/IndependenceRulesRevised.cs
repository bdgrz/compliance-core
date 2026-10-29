using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.independence.rules.revised", 2)]
public sealed record IndependenceRulesRevised(IndependenceRuleCatalogHeader Header, ImmutableContentRef RuleContent,
    long NativeSequence, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact
{
    public Uuid RequestId => Operation.LogicalId;
    public long ExpectedSequence => checked((long)Operation.OriginalExpectedRevision!.Value);
}
