

using System.Threading.Channels;
using IronPython.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using libplctag;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace Derail;

class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                
                // EIP.ClientService -> bunker-micro
                
                services.Configure<EIP.ClientServiceOptions>("bunker-micro", o =>
                {
                    o.Name = "bunker-micro";
                    o.Enabled = false;
                    o.Gateway = "192.168.111.20";
                    o.Protocol = Protocol.ab_eip;
                    o.PlcType = PlcType.MicroLogix;
                    o.Timeout = TimeSpan.FromMilliseconds(5000);
                    o.ReadInterval = TimeSpan.FromMilliseconds(1000);
                    o.Tags = new()
                    {
                        new EIP.Tag() { Mapper = "TagBool", Name = "B3:0/2" },
                        new EIP.Tag() { Mapper = "TagBool", Name = "B3:0/3" }
                    };
                });
                
                services.AddSingleton<IHostedService>(sp =>
                    ActivatorUtilities.CreateInstance<EIP.ClientService>(sp, "bunker-micro")
                );
                
                
                // MQTT.ClientService -> wss.sharc.tech
                
                services.Configure<MQTT.ClientServiceOptions>("sharc-mqtt", o =>
                {
                    o.Name = "sharc-mqtt";
                    o.Enabled = true;
                    o.BrokerAddress = "wss.sharc.tech";
                    o.BrokerPort = 1883;
                    o.UseTls = false;
                    o.Username = "";
                    o.Password = "";
                    o.ClientId = Guid.NewGuid().ToString();
                    o.ReconnectInterval = TimeSpan.FromMilliseconds(10000);
                    o.SubscriptionTopics = new() { "sharc/+/evt/#" };
                });
                
                services.AddSingleton<IHostedService>(sp =>
                    ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "sharc-mqtt")
                );
                
                
                // MQTT.ClientService -> mosquitto.spb.mtcup.org
                
                services.Configure<MQTT.ClientServiceOptions>("mtcup-mqtt", o =>
                {
                    o.Name = "mtcup-mqtt";
                    o.Enabled = false;
                    o.BrokerAddress = "mosquitto.spb.mtcup.org";
                    o.BrokerPort = 1884;
                    o.UseTls = false;
                    o.Username = "";
                    o.Password = "";
                    o.ClientId = Guid.NewGuid().ToString();
                    o.ReconnectInterval = TimeSpan.FromMilliseconds(10000);
                    o.SubscriptionTopics = new() { "#" };
                });
                
                services.AddSingleton<IHostedService>(sp =>
                    ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "mtcup-mqtt")
                );
                
                
                // DeviceCache.ClientService
                
                services.AddHostedService<DeviceCache.ClientService>();
                
                
                // TerminatorService
                
                /*
                    How to stop application: Complete the terminator service inbound channel from any service.
                        _tsChannelWriter.Complete();
                */
                
                services.AddHostedService<TerminatorService>();

                services.AddSingleton(sp => new TerminatorService.TerminatorServiceOptions()
                {
                    TerminateInMs = 0   // 0 = do not terminate using timer
                });
                
                addChannels(services);
                
            })
            .ConfigureLogging((context, builder) =>
            {
                builder.ClearProviders();
                builder.AddNLog(context.Configuration);
            })
            .RunConsoleAsync();
    }

    static void addChannels(IServiceCollection services)
    {
        // System
        
        services.AddSingleton<Channel<SystemControlFrame>>(
            Channel.CreateUnbounded<SystemControlFrame>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        services.AddSingleton<ChannelWriter<SystemControlFrame>>(
            sp => sp.GetRequiredService<Channel<SystemControlFrame>>().Writer);
                
        services.AddSingleton<ChannelReader<SystemControlFrame>>(
            sp => sp.GetRequiredService<Channel<SystemControlFrame>>().Reader);
                
        services.AddSingleton<Channel<SystemMessageFrame>>(
            Channel.CreateUnbounded<SystemMessageFrame>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        services.AddSingleton<ChannelWriter<SystemMessageFrame>>(
            sp => sp.GetRequiredService<Channel<SystemMessageFrame>>().Writer);
                
        services.AddSingleton<ChannelReader<SystemMessageFrame>>(
            sp => sp.GetRequiredService<Channel<SystemMessageFrame>>().Reader);
        
        // TerminatorService
        
        services.AddSingleton<Channel<bool>>(
            Channel.CreateUnbounded<bool>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        services.AddSingleton<ChannelWriter<bool>>(
            sp => sp.GetRequiredService<Channel<bool>>().Writer);
                
        services.AddSingleton<ChannelReader<bool>>(
            sp => sp.GetRequiredService<Channel<bool>>().Reader);
    }
}