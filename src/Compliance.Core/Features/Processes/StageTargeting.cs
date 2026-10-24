namespace Bdgrz.Compliance.Features.Processes;

/// <summary>How a stage of an ordered process walks the canonical targets frozen on its record.</summary>
public enum StageTargeting
{
    /// <summary>The stage name decides: the acquiring, claiming, releasing and settling stages walk every target.</summary>
    ByStageName,

    /// <summary>The stage dispatches each receiver once, without a target.</summary>
    Once,

    /// <summary>Each receiver is dispatched for every canonical target in turn, receiver by receiver.</summary>
    EachReceiver,

    /// <summary>
    ///     Each canonical target is offered to every receiver in turn, target by target, so one global key order is
    ///     acquired even when different receivers own different kinds of key. A receiver that does not own the target
    ///     builds no command and the driver skips it.
    /// </summary>
    Interleaved,
}
