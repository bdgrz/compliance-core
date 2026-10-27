using Bdgrz.Compliance.Features.Processes.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
///     Builds the typed receiver command of one step, or null when the step does not apply to this record (for
///     example the receivers a transition kind did not select). It may read the record's frozen intent and prior
///     receiver facts, and resolves what it needs from the scoped provider of the driver.
/// </summary>
public delegate ValueTask<IRequest<ReceiverOutcome>?> ProcessReceiverFactory(ProcessStep step,
    IServiceProvider services, CancellationToken ct);
