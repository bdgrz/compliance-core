using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>Records the approve type iioperating plan intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.approve-type-iioperating-plan", 1)]
public sealed record ApproveTypeIIOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid PlanId, long Version, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
