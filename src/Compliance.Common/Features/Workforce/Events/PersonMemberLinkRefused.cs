using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.person.member-link-refused", 1)]
public sealed record PersonMemberLinkRefused(PersonHeader Header, ImmutableContentRef Content, ImmutableContentRef Change,
    bool Changed, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    PersonMemberLink Link, string Reason) : DomainEvent, IPersonNativeFact;
