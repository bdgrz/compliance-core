using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Processes;

/// <summary>One technical export authority; attempts and output never become description business facts.</summary>
public sealed record SystemDescriptionExportProcessRecord(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    AcceptedOperationBinding Binding, ImmutableContentRef Intent, string State, int Attempts, long Revision,
    ImmutableContentRef? Output, ImmutableContentRef? OperationResult, string? FailureCode);
