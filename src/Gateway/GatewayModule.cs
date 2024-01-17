using CoreFlex.Jwt;
using CoreFlex.Module;

namespace Gateway;

[DependOns(typeof(CoreFlexJwtModule))]
public class GatewayModule : CoreFlexModule
{
    public override void ConfigureServices(CoreFlexServiceContext context)
    {
        
    }
}