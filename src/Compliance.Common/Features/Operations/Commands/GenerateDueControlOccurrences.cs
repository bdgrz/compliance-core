using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>Records the generate due control occurrences intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.generate-due-control-occurrences", 1)]
public sealed record GenerateDueControlOccurrences(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, DateOnly ThroughDate, string GenerationKey) : IRequest<UnitResult>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
