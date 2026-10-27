using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Models;

/// <summary>The exact native evaluation authority used to prepare one immutable result.</summary>
sealed record EvaluationPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static EvaluationPreparedCut Capture(ControlEvaluation owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(ControlEvaluation owner) => this == Capture(owner);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The control evaluation authority changed while immutable content was prepared.", true));
}
