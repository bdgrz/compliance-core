using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Events;

[Discriminator("bdgrz.criterion_applicability.not_applicable_proposed", 3)]
public sealed record CriterionNotApplicableProposed(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid EditionId, string CriterionIdentifier, Uuid ProposerMemberId,
    ActorReference Actor, DateTimeOffset ProposedAt, ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IMappingLineFact;
