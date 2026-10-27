using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The duty-designation pair of the generic settle receiver. The terminal fact is resolved from the
///     engagement owner; a settlement before any grant fences the key so a delayed acquisition is refused.
/// </summary>
sealed class SettleFirmProfessionalDutyDesignationEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseSettleHandler<FirmProfessionalDutyDesignation, SettleFirmProfessionalDutyDesignationEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(SettleFirmProfessionalDutyDesignationEngagementUse);

    protected override FirmProfessionalDutyDesignation Open(SettleFirmProfessionalDutyDesignationEngagementUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, FirmProfessionalDuty.EngagementPartner,
            EngagementPersonalDecisionVerifier.TenantOf(request.Source.Stream), selectedCommandId);

    protected override Func<FirmProfessionalDutyDesignation, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (FirmProfessionalDutyDesignation owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettleEngagementUse(provenance, intent, generation);
}
