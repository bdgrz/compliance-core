using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Hosting.Persistence;

static class FitzFrameLimits
{
    // The application keeps a 65,535-byte operation cap, including route metadata.
    // Fitz 4 encodes these extension-free operation payloads inside its larger Wire 3 frames.
    public static void CheckEvent(EventStreamAddress stream, int envelopeBytes)
    {
        var size = (long)envelopeBytes + Encoding.UTF8.GetByteCount(stream.ToString()) + 26;
        if (size > ushort.MaxValue)
            throw new EventFrameSizeException(size);
    }

    public static void CheckKv(string route, long operationBytesWithoutRoute)
    {
        var size = operationBytesWithoutRoute + Encoding.UTF8.GetByteCount(route);
        if (size > ushort.MaxValue)
            throw new KvFrameSizeException(size);
    }
}
