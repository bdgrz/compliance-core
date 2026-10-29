using Microsoft.AspNetCore.Hosting;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Tests;

static class ProductionEmailDeliveryTestConfiguration
{
    public static void Apply(IWebHostBuilder builder)
    {
        builder.UseSetting("Compliance:EmailDelivery:Mode", "smtp");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:Host", "smtp.example.com");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:Port", "587");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:FromAddress", "verify@example.com");
        builder.UseSetting("Compliance:EmailDelivery:ActiveTokenKeyId", "test-key");
        builder.UseSetting("Compliance:EmailDelivery:TokenKeys:test-key",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
    }
}
