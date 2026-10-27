using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Services;

/// <summary>Recomputes a flow's carried classification under a proposed asset change (M0-D08).</summary>
public static class InformationAssetImpact
{
    public static DataFlowImpactView Evaluate(DataFlowView flow, Uuid changedAssetId,
        string proposedClassification, string proposedLifecycle,
        IReadOnlyDictionary<Uuid, InformationAssetContent> carried)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(carried);
        var classifications = new List<string>();
        var anyActive = false;
        foreach (var assetId in flow.Content.InformationAssetIds)
        {
            var (classification, lifecycle) = assetId == changedAssetId
                ? (proposedClassification, proposedLifecycle)
                : carried.TryGetValue(assetId, out var content)
                    ? (content.Classification, content.Lifecycle)
                    // An unknown asset is ranked by the flow's recorded value so it never lowers it.
                    : (flow.Content.Classification, TechnologyInventoryRules.Active);
            classifications.Add(classification);
            anyActive |= lifecycle == TechnologyInventoryRules.Active;
        }
        var recomputed = classifications.MaxBy(TechnologyInventoryRules.Rank) ??
                         flow.Content.Classification;
        var violation = TechnologyInventoryRules.RequiresEncryption(recomputed) &&
                        !(flow.Content.EncryptedInTransit && flow.Content.EncryptedAtRest) &&
                        flow.Content.ExceptionReference is null;
        return new DataFlowImpactView(flow.DataFlowId, flow.Revision, flow.Content.Classification,
            recomputed, recomputed != flow.Content.Classification,
            flow.Content.EncryptedInTransit, flow.Content.EncryptedAtRest,
            flow.Content.ExceptionReference, violation, !anyActive);
    }
}
