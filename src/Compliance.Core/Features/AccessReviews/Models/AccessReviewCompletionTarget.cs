using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     One target of the completion process: the item's stream resource, which orders the targets canonically, and
///     the item id the receivers open it by. The process factory is given only this one string per step, so it carries
///     everything a receiver command needs beyond the root source fact.
/// </summary>
readonly record struct AccessReviewCompletionTarget(string Resource, Uuid ItemId)
{
    const char Separator = '|';

    public static AccessReviewCompletionTarget Of(Uuid tenantId, Uuid campaignId, Uuid itemId) =>
        new(AccessReviewEffects.ItemStreamId(tenantId, campaignId, itemId).ToString(), itemId);

    public static AccessReviewCompletionTarget Parse(string value)
    {
        var parts = value.Split(Separator);
        if (parts.Length != 2)
            throw new FormatException("A completion target has a stream resource and an item id.");
        return new AccessReviewCompletionTarget(parts[0], Uuid.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"{Resource}{Separator}{ItemId}";
}
