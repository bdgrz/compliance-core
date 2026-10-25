using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Native identified person receipt; complete values and mutation proof are immutable content.</summary>
[Discriminator("bdgrz.workforce.person.membership-correlated", 2)]
public sealed record PersonMembershipCorrelationRecorded(PersonHeader Header, ImmutableContentRef Content, ImmutableContentRef Change,
    bool Changed, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, IPersonNativeFact;
