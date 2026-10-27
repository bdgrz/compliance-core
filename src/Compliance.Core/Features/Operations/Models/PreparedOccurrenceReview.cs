using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>The fresh exact occurrence and optional independent-review waiver checked only after native retry resolution.</summary>
sealed record PreparedOccurrenceReview(ControlOccurrenceView Current, SeparationOfDutiesWaiver? Waiver);
