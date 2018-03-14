using Owin;
using Microsoft.Owin;
using Microsoft.Owin.Cors;

[assembly: OwinStartup(typeof(ServerFileSync.Startup))]
namespace ServerFileSync
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Any connection or hub wire up and configuration should go here
            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR(new Microsoft.AspNet.SignalR.HubConfiguration() { EnableDetailedErrors = true, EnableJavaScriptProxies = true, EnableJSONP = true });
            //app.MapSignalR();
        }
    }
}