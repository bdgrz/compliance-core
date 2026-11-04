using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>An Org Admin's immutable decision on any exact complete assessment.</summary>
[Discriminator("bdgrz.readiness.type_i_entry.decide", 3)]
public sealed record DecideTypeIEntryV3(Uuid DecisionId, Uuid TenantId, Uuid ProgramId,
    Uuid AssessmentId, string Outcome, string Rationale)
    : IRequest<TypeIEntryDecisionRecord>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
