using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>Only public display text from a unique explicit tenant-local correlation.</summary>
public interface IPersonMemberDisplayReader
{
    ValueTask<string?> ReadAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default);
}
