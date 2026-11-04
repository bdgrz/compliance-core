using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.person.linked-to-member", 1)]
public sealed record PersonLinkedToMember(PersonHeader Header, ImmutableContentRef Content, ImmutableContentRef Change,
    bool Changed, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    PersonMemberLink Link) : DomainEvent, IPersonNativeFact;
