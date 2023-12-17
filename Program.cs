namespace Derail;

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // MQTT.ClientService -> wss.sharc.tech
                
                services.Configure<MQTT.ClientServiceOptions>("wss.sharc.tech", o =>
                {
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
                    ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "wss.sharc.tech")
                );
                
                
                // MQTT.ClientService -> mosquitto.spb.mtcup.org
                
                services.Configure<MQTT.ClientServiceOptions>("mosquitto.spb.mtcup.org", o =>
                {
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
                    ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "mosquitto.spb.mtcup.org")
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
                    TerminateInMs = 10000   // 0 = do not terminate using timer
                });
                
                addChannels(services);
                
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