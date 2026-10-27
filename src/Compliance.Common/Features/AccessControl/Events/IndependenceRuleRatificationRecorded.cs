using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.independence.rules.ratification-recorded", 2)]
public sealed record IndependenceRuleRatificationRecorded(IndependenceRuleRatificationHeader Header, ImmutableContentRef RatificationContent,
    long NativeSequence, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact
{
    public Uuid RequestId => Operation.LogicalId;
    public long ExpectedSequence => checked((long)Operation.OriginalExpectedRevision!.Value);
}
