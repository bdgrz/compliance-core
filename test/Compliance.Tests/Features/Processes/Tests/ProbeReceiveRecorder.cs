using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

/// <summary>Records every dispatched receiver command and scripts its outcome.</summary>
sealed class ProbeReceiveRecorder
{
    readonly List<ProbeReceive> _calls = [];
    readonly Dictionary<string, Func<ProbeReceive, Result<ReceiverOutcome>>> _scripts = new(StringComparer.Ordinal);

    public IReadOnlyList<ProbeReceive> Calls => _calls;

    public void Script(string receiver, Func<ProbeReceive, Result<ReceiverOutcome>> outcome) =>
        _scripts[receiver] = outcome;

    public Result<ReceiverOutcome> Handle(ProbeReceive command)
    {
        _calls.Add(command);
        return _scripts.TryGetValue(command.Receiver, out var script)
            ? script(command)
            : Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(new(
                new EventStreamAddress("tenant-a", "targets", command.Target ?? command.Receiver), command.CommandId, 1)));
    }
}
