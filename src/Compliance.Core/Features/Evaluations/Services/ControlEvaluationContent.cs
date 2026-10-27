using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Evaluations.Services;

/// <summary>The schemas of the immutable content that evaluation facts and first responses reference.</summary>
static class ControlEvaluationContent
{
    /// <summary>The complete frozen procedure of a start.</summary>
    public const string StepsSchema = "ControlEvaluationStarted.StepsManifest.v1";

    /// <summary>The complete list of retested deviation ids of a start.</summary>
    public const string RetestDeviationsSchema = "ControlEvaluationStarted.RetestOfDeviationIdsManifest.v1";

    /// <summary>The result of one step with its complete inspected items.</summary>
    public const string ResultSchema = "ControlEvaluationStepRecorded.ResultContent.v1";

    /// <summary>The deviation a not_met step raised.</summary>
    public const string DeviationSchema = "ControlEvaluationStepRecorded.DeviationContent.v1";

    /// <summary>The complete submitted round.</summary>
    public const string SubmissionSchema = "ControlEvaluationSubmitted.SubmissionContent.v1";

    /// <summary>The complete review decision.</summary>
    public const string ReviewSchema = "ControlEvaluationReviewed.ReviewContent.v1";

    /// <summary>The evaluation view an operation returns.</summary>
    public const string ViewSchema = "ControlEvaluation.View.v1";
}
