using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace Derail;

class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        // SERVICES
        
        //  EIP.ClientService -> bunker-micro
        
        builder.Services.Configure<EIP.ClientServiceOptions>("bunker-micro", o =>
        {
            o.Name = "bunker-micro";
            o.Enabled = true;
            o.Gateway = "192.168.111.20";
            o.Protocol = libplctag.Protocol.ab_eip;
            o.PlcType = libplctag.PlcType.MicroLogix;
            o.Timeout = TimeSpan.FromMilliseconds(5000);
            o.ReadInterval = TimeSpan.FromMilliseconds(1000);
            o.BackoffOnTimeout = TimeSpan.FromMilliseconds(60000);
            o.RemoveTagFromReadPoolOnError = false;
            o.Tags = new()
            {
                new EIP.Tag() { Enabled = true, Mapper = "TagBool", Name = "B3:0/2" },
                new EIP.Tag() { Enabled = true, Mapper = "TagBool", Name = "B3:0/3" }
            };
        });
        
        builder.Services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<EIP.ClientService>(sp, "bunker-micro")
        );
        
        //  MQTT.ClientService -> wss.sharc.tech
                
        builder.Services.Configure<MQTT.ClientServiceOptions>("sharc-mqtt", o =>
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
            o.SubscriptionTopics = new()
            {
                new MQTT.SubscriptionTopic() { Enabled = true, Topic = "sharc/+/evt/#" }
            };
        });
        
        builder.Services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "sharc-mqtt")
        );
        
        //  MQTT.ClientService -> mosquitto.spb.mtcup.org
        
        builder.Services.Configure<MQTT.ClientServiceOptions>("mtcup-mqtt", o =>
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
            o.SubscriptionTopics = new()
            {
                new MQTT.SubscriptionTopic() { Enabled = true, Topic = "#" }
            };
        });
        
        builder.Services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<MQTT.ClientService>(sp, "mtcup-mqtt")
        );
        
        //  DeviceCache.ClientService
        
        builder.Services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<DeviceCache.ClientService>(sp, "device-cache")
        );
        
        //  TerminatorService
        
        /*
            How to stop application: Complete the terminator service inbound channel from any service.
                _terminatorChannelWriter.Complete();
        */
        
        builder.Services.Configure<Terminator.ServiceOptions>("terminator", o =>
        {
            o.TerminateInMs = 0;    // 0 = do not terminate using timer
        });
        
        builder.Services.AddSingleton<IHostedService>(sp =>
            ActivatorUtilities.CreateInstance<Terminator.Service>(sp, "terminator")
        );
        
        // CHANNELS
        
        //  System
        
        builder.Services.AddSingleton<Channel<SystemControlFrame>>(
            Channel.CreateUnbounded<SystemControlFrame>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        builder.Services.AddSingleton<ChannelWriter<SystemControlFrame>>(
            sp => sp.GetRequiredService<Channel<SystemControlFrame>>().Writer);
                
        builder.Services.AddSingleton<ChannelReader<SystemControlFrame>>(
            sp => sp.GetRequiredService<Channel<SystemControlFrame>>().Reader);
                
        builder.Services.AddSingleton<Channel<SystemMessageFrame>>(
            Channel.CreateUnbounded<SystemMessageFrame>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        builder.Services.AddSingleton<ChannelWriter<SystemMessageFrame>>(
            sp => sp.GetRequiredService<Channel<SystemMessageFrame>>().Writer);
                
        builder.Services.AddSingleton<ChannelReader<SystemMessageFrame>>(
            sp => sp.GetRequiredService<Channel<SystemMessageFrame>>().Reader);
        
        //  TerminatorService
        
        builder.Services.AddSingleton<Channel<bool>>(
            Channel.CreateUnbounded<bool>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false }));

        builder.Services.AddSingleton<ChannelWriter<bool>>(
            sp => sp.GetRequiredService<Channel<bool>>().Writer);
                
        builder.Services.AddSingleton<ChannelReader<bool>>(
            sp => sp.GetRequiredService<Channel<bool>>().Reader);
        
        // Finalize Host
        
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();
        
        IHost host = builder.Build();
        
        await host.RunAsync();
        
        NLog.LogManager.Shutdown();
    }
}

/*
// https://github.com/aaubry/YamlDotNet/wiki/Serialization.Deserializer

var yaml = File.ReadAllText("config.yml");
var stringReader = new StringReader(yaml);
var parser = new Parser(stringReader);
var mergingParser = new MergingParser(parser);
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();
dynamic config = deserializer.Deserialize(mergingParser);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

foreach (var service in config["Service"])
{
    Type instanceType = Type.GetType(service["InstanceType"]);
    Type optionsType = Type.GetType(service["OptionsType"]);

    builder.Services.Configure(
        (string)service["Name"],
        o => new EIP.ClientServiceOptions());
}
*/
