using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Tests;

[PortiaJsonContext]
[JsonSerializable(typeof(RegisterTenant))]
[JsonSerializable(typeof(TenantRegistration))]
[JsonSerializable(typeof(RegisterMember))]
[JsonSerializable(typeof(DeleteTeam))]
[JsonSerializable(typeof(UnprotectedCompositionRequest))]
[JsonSerializable(typeof(Features.NativeOperations.Tests.ProbeAdmitted))]
[JsonSerializable(typeof(Features.NativeOperations.Tests.ProbeEdited))]
[JsonSerializable(typeof(Features.Processes.Tests.ProbeReceive))]
[JsonSerializable(typeof(Testing.TypedReactionBus.TypedRequest))]
sealed partial class RequestScenarioJsonContext : JsonSerializerContext;
