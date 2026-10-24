namespace Bdgrz.Compliance.Features.Evaluations;

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
