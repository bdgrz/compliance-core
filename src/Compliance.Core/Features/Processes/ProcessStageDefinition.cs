namespace Bdgrz.Compliance.Features.Processes;

/// <summary>One stage of an ordered process and the receivers it dispatches.</summary>
public sealed record ProcessStageDefinition(string Stage, IReadOnlyList<string> Receivers, string? Rule = null)
{
    /// <summary>Stages that acquire keys dispatch once per canonical target; others dispatch once.</summary>
    public bool IsPerTarget => Stage is "acquire" or "reserve" or "claim" or "authorize" or "release" or "settle";

    /// <summary>Stages that still run after an abort has fenced the receiving operation.</summary>
    public bool RunsAfterAbort => Stage is "settle" or "release";
}
