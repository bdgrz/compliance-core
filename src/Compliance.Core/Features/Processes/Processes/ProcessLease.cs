using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>Worker lease on a record. It only steers which worker resumes; expiry never releases authority.</summary>
public sealed record ProcessLease(string Worker, DateTimeOffset Until);
