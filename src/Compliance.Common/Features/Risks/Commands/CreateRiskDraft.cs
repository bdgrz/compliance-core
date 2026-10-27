using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Creates the risk under its authored UUIDv5 tenant/program/normalized-identifier identity; mismatches are rejected.</summary>
[Discriminator("bdgrz.risk.draft.create", 1)]
public sealed record CreateRiskDraft(Uuid RiskId, Uuid TenantId, Uuid ProgramId, string Identifier,
    string Title, string Scenario, string PotentialEffect, string? SourceNote = null)
    : IRequest<RiskRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => RiskId;
}
