using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>Isolates the process-wide SmtpClient TLS callback from every other test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SmtpInvitationCollectionDefinition
{
    public const string Name = "SMTP invitation delivery";
}
