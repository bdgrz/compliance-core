using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
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
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Services;

static class VisibleApplicationPage
{
    public static async ValueTask<Page<T>> ReadAsync<T>(int limit, string? cursor,
        Func<int, string?, ValueTask<Page<T>>> read,
        Func<T, ValueTask<bool>> isVisible, Func<T, bool> belongsToRequest)
    {
        var visible = new List<T>(limit);
        var scanCursor = cursor;
        while (true)
        {
            var page = await read(limit - visible.Count, scanCursor).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (!belongsToRequest(item))
                    throw new ForeignDirectoryItemException();
                if (await isVisible(item).ConfigureAwait(false))
                    visible.Add(item);
            }

            if (visible.Count == limit || page.NextCursor is null)
                return new Page<T>(visible, page.NextCursor);
            scanCursor = page.NextCursor;
        }
    }

    public sealed class ForeignDirectoryItemException : Exception
    {
    }
}
