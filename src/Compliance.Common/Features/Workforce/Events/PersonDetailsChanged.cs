using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Events;

/// <summary>Named changed details, with actual values in the existing verified native person content.</summary>
[Discriminator("bdgrz.workforce.person.details.changed", 1)]
public sealed record PersonDetailsChanged(PersonHeader Header, ImmutableContentRef Content, ImmutableContentRef Change,
    bool Changed, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    PersonDetailChanges ChangedFields) : DomainEvent, IPersonNativeFact;
