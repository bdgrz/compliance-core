using System.Globalization;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

sealed record WorkDigestSourceItem(Uuid ProgramId, string ProgramName,
    WorkQueueItemView Item);

static class WorkDigestContentComposer
{
    public static WorkDigestDeliveryMessage? Compose(Uuid tenantId, Uuid memberId,
        DateOnly weekOf, string tenantSlug, Uuid messageId, string recipient,
        IEnumerable<WorkDigestSourceItem> sourceItems, WorkDigestDeliverySettings settings)
    {
        var selected = sourceItems
            .Where(candidate => candidate.Item.AssigneeMemberId == memberId &&
                candidate.Item.DueOn is { } dueOn &&
                (dueOn < weekOf || dueOn <= weekOf.AddDays(7)))
            .DistinctBy(static candidate => (candidate.ProgramId, candidate.Item.WorkItemId))
            .OrderBy(static candidate => candidate.Item.DueOn)
            .ThenBy(static candidate => candidate.ProgramId.ToString(), StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Item.WorkItemId.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0)
            return null;

        var lines = new StringBuilder();
        lines.Append("Weekly work digest for the week of ")
            .Append(weekOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .AppendLine().AppendLine();
        AppendSection(lines, "Overdue", selected.Where(candidate => candidate.Item.DueOn < weekOf),
            tenantId, tenantSlug, settings);
        AppendSection(lines, "Due soon", selected.Where(candidate => candidate.Item.DueOn >= weekOf),
            tenantId, tenantSlug, settings);
        lines.AppendLine("Open each item in the application to review its current status and access.");
        return new WorkDigestDeliveryMessage(messageId, recipient, "Your weekly work digest",
            lines.ToString());
    }

    static void AppendSection(StringBuilder lines, string title,
        IEnumerable<WorkDigestSourceItem> candidates, Uuid tenantId, string tenantSlug,
        WorkDigestDeliverySettings settings)
    {
        var items = candidates.ToArray();
        if (items.Length == 0)
            return;
        lines.AppendLine(title + ":");
        foreach (var candidate in items)
        {
            var dueOn = candidate.Item.DueOn!.Value;
            var link = settings.CreateWorkItemUri(tenantId, tenantSlug, candidate.ProgramId,
                candidate.Item.WorkItemId);
            if (link is null)
                throw new InvalidOperationException("The weekly digest client link is invalid.");
            lines.Append("- ").Append(OneLine(candidate.ProgramName)).Append(": ")
                .Append(OneLine(candidate.Item.Summary)).Append(" (due ")
                .Append(dueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).AppendLine(")")
                .Append("  ").AppendLine(link.AbsoluteUri);
        }
        lines.AppendLine();
    }

    static string OneLine(string value) => string.Join(' ', value.Split(['\r', '\n'],
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
