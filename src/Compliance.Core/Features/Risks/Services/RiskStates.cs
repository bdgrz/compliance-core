using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>The outcome, trigger and action status vocabulary shared by the risk governance owners and their readers.</summary>
public static class RiskStates
{
    /// <summary>The outcome that accepts a treatment assertion or an action completion.</summary>
    public const string Accept = "accept";

    /// <summary>The outcome that rejects a treatment assertion or an action completion.</summary>
    public const string Reject = "reject";

    /// <summary>The trigger kind raised when a later risk method version is published.</summary>
    public const string MethodChanged = "method_changed";

    /// <summary>The trigger kind raised when a successor boundary version is approved.</summary>
    public const string BoundaryChanged = "boundary_changed";

    /// <summary>The status of a treatment action with open work.</summary>
    public const string ActionOpen = "open";

    /// <summary>The status of a treatment action whose completion awaits independent review.</summary>
    public const string ActionSubmitted = "completion_submitted";

    /// <summary>The status of a treatment action whose completion was independently accepted.</summary>
    public const string ActionCompleted = "completed";

    /// <summary>The status of a canceled treatment action.</summary>
    public const string ActionCancelled = "cancelled";

    /// <summary>The status of a proposed control treatment assertion.</summary>
    public const string TreatmentProposed = "proposed";

    /// <summary>The status of an accepted control treatment assertion.</summary>
    public const string TreatmentAccepted = "accepted";

    /// <summary>The status of a rejected control treatment assertion.</summary>
    public const string TreatmentRejected = "rejected";

    /// <summary>The status of an accepted assertion replaced by a newer accepted version of the same control.</summary>
    public const string TreatmentSuperseded = "superseded";

    /// <summary>The status of a retired control treatment assertion.</summary>
    public const string TreatmentRetired = "retired";
}
