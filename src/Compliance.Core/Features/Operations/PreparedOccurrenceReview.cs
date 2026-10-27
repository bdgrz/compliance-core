using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The fresh exact occurrence and optional independent-review waiver checked only after native retry resolution.</summary>
sealed record PreparedOccurrenceReview(ControlOccurrenceView Current, SeparationOfDutiesWaiver? Waiver);
