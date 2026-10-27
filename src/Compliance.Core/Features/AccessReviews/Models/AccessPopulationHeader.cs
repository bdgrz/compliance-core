using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>The frozen identity of an accepted population: what was observed, where, when, and how.</summary>
public sealed record AccessPopulationHeader(Uuid PopulationId, Uuid ApplicationId,
    Uuid SystemInstanceId, long SystemInstanceRevision, DateTimeOffset ObservedAt,
    string SourceKind, string Source, string Attestation);
