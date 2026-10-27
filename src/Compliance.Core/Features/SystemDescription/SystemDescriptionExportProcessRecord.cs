using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>One technical export authority; attempts and output never become description business facts.</summary>
public sealed record SystemDescriptionExportProcessRecord(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    AcceptedOperationBinding Binding, ImmutableContentRef Intent, string State, int Attempts, long Revision,
    ImmutableContentRef? Output, ImmutableContentRef? OperationResult, string? FailureCode);
