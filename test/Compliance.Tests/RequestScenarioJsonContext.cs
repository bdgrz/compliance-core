using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

[PortiaJsonContext]
[JsonSerializable(typeof(RegisterTenant))]
[JsonSerializable(typeof(TenantRegistration))]
[JsonSerializable(typeof(RegisterMember))]
[JsonSerializable(typeof(DeleteTeam))]
[JsonSerializable(typeof(UnprotectedCompositionRequest))]
[JsonSerializable(typeof(Features.NativeOperations.ProbeAdmitted))]
[JsonSerializable(typeof(Features.NativeOperations.ProbeEdited))]
[JsonSerializable(typeof(Features.Processes.ProbeReceive))]
sealed partial class RequestScenarioJsonContext : JsonSerializerContext;
