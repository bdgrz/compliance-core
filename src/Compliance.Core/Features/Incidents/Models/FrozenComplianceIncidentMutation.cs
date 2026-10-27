using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Verified preparation data consumed synchronously by the native owner at its exact cut.</summary>
public sealed record FrozenComplianceIncidentMutation(ComplianceIncidentView? Before, ComplianceIncidentView After,
    ComplianceIncidentCurrentAuthority? BeforeAuthority, ComplianceIncidentCurrentAuthority Authority,
    ImmutableContentRef CurrentContent, ImmutableContentRef Change, ComplianceIncidentMutation Mutation, object Payload,
    ComplianceIncidentAuthorityHeader Header);
