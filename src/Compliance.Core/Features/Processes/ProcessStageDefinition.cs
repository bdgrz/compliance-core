namespace Bdgrz.Compliance.Features.Processes;

/// <summary>One stage of an ordered process and the receivers it dispatches.</summary>
public sealed record ProcessStageDefinition(string Stage, IReadOnlyList<string> Receivers, string? Rule = null)
{
    /// <summary>
    ///     How the stage walks the record's canonical targets. The default follows the stage name, which is how every
    ///     process before the interleaved stage behaves.
    /// </summary>
    public StageTargeting Targeting { get; init; } = StageTargeting.ByStageName;

    /// <summary>Stages that acquire keys dispatch once per canonical target; others dispatch once.</summary>
    public bool IsPerTarget => Targeting switch
    {
        StageTargeting.Once => false,
        StageTargeting.EachReceiver or StageTargeting.Interleaved => true,
        _ => Stage is "acquire" or "reserve" or "claim" or "authorize" or "release" or "settle",
    };

    /// <summary>
    ///     True when the canonical targets are the outer loop and the receivers the inner one, so one global key order
    ///     is walked and every target offers itself to every receiver in turn.
    /// </summary>
    public bool IsInterleaved => Targeting == StageTargeting.Interleaved;

    /// <summary>Stages that still run after an abort has fenced the receiving operation.</summary>
    public bool RunsAfterAbort => Stage is "settle" or "release";
}
