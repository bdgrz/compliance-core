using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>Publishes complete current authority and operation changes without putting read-history snapshots in the owner.</summary>
public static class ControlContent
{
    public static async ValueTask<FrozenControlMutation> FreezeAsync(IImmutableContentStore content,
        ControlRules after, ControlMutationContent mutation, CancellationToken ct = default)
    {
        if (after.CurrentContent is not { } current || ControlRules.ValidateContent(current) is not null)
            throw new ImmutableContentIntegrityException("The prepared Control has no valid authored current content.");
        var realm = after.TenantId.ToString();
        var draft = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            ControlFactContent.DraftSchema + ":" + AuthoredIntentDigest.Compute(current)),
            ControlFactContent.DraftSchema, current, ct).ConfigureAwait(false);
        var header = after.Header(draft);
        var state = after.Authority(header);
        var authority = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            ControlFactContent.AuthoritySchema + ":" + AuthoredIntentDigest.Compute(state)),
            ControlFactContent.AuthoritySchema, state, ct).ConfigureAwait(false);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            ControlFactContent.ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new FrozenControlMutation(header, change, authority);
    }
}
