using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Derail.MQTT;

public class ClientService: IHostedService
   {
      private readonly string _instanceId;
      private readonly ClientServiceOptions _serviceOptions;
      private readonly IHostApplicationLifetime _appLifetime;
      private ChannelWriter<SystemControlFrame> _channelControlWriter;
      private ChannelWriter<SystemMessageFrame> _channelMessageWriter;
      
      private IMqttClient _client;
      
      private Task _task1;
      private CancellationTokenSource _tokenSource1;
      private CancellationToken _token1;
      
      public ClientService(
         string instanceId,
         IOptionsMonitor<ClientServiceOptions> optionsMonitor,
         IHostApplicationLifetime appLifetime,
         ChannelWriter<SystemControlFrame> channelSystemWriter,
         ChannelWriter<SystemMessageFrame> channelMessageWriter)
      {
         _instanceId = instanceId;
         _serviceOptions = optionsMonitor.Get(instanceId);
         _appLifetime = appLifetime;
         _channelControlWriter = channelSystemWriter;
         _channelMessageWriter = channelMessageWriter;
      }
      
      public Task StartAsync(CancellationToken cancellationToken)
      {
         _appLifetime.ApplicationStarted.Register(() =>
         {
            _tokenSource1 = new CancellationTokenSource();
            _token1 = _tokenSource1.Token;
            
            _task1 = Task.Run(async () =>
            {
               try
               {
                  await ConnectToBroker();
                  
                  while (!_token1.IsCancellationRequested)
                  {
                     await Task.Yield();
                  }

                  await DisconnectFromBroker();
               }
               catch (Exception ex)
               {
                  Console.WriteLine("MQTT.ClientService CLIENT ERROR");
                  Console.WriteLine(ex);
                  
                  await WriteOutboundControlFrame("ERROR", ex);
               }
               finally
               {
                  Console.WriteLine("MQTT.ClientService CLIENT Stopping");
                  _appLifetime.StopApplication();
               }
            }, _token1);
         });

         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         Console.WriteLine("MQTT.ClientService Stop");

         _tokenSource1.Cancel();
         Task.WaitAll(_task1);
         
         return Task.CompletedTask;
      }

      /*private async Task ProcessInboundFrame(ClientServiceInboundChannelFrame frame)
      {
         Console.WriteLine(frame.Type);
         
      }*/

      private async Task WriteOutboundControlFrame(string @event, dynamic? data = null)
      {
         Console.WriteLine($"MQTT.ClientService Write Frame: CONTROL/{@event}");
         await _channelControlWriter.WriteAsync(new SystemControlFrame()
         {
            SourceInstanceId = _instanceId,
            Payload = new { @event, data }
         });
      }
      
      private async Task WriteOutboundMessageFrame(dynamic payload)
      {
         Console.WriteLine($"MQTT.ClientService Write Frame: MESSAGE");
         await _channelMessageWriter.WriteAsync(new SystemMessageFrame()
         {
            SourceInstanceId = _instanceId,
            Payload = payload
         });
      }
      
      private async Task ConnectToBroker()
      {
         _client = new MqttFactory().CreateMqttClient();
         _client.DisconnectedAsync += ClientOnDisconnectedAsync;
         _client.ApplicationMessageReceivedAsync += OnClientOnApplicationMessageReceivedAsync;
        
         var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_serviceOptions.BrokerAddress, _serviceOptions.BrokerPort)
            .Build();
         
         try
         {
            await WriteOutboundControlFrame("CONNECTING");
            var response = await _client.ConnectAsync(mqttClientOptions, CancellationToken.None);
            if (response.ResultCode == MqttClientConnectResultCode.Success)
            {
               var mqttSubscribeOptionsBuilder = new MqttFactory().CreateSubscribeOptionsBuilder();
               foreach (var subscriptionTopic in _serviceOptions.SubscriptionTopics)
               {
                  mqttSubscribeOptionsBuilder.WithTopicFilter(subscriptionTopic, MqttQualityOfServiceLevel.AtMostOnce);
               }
               var mqttSubscribeOptions = mqttSubscribeOptionsBuilder.Build();
                  
               await _client.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
               await WriteOutboundControlFrame("CONNECTED");
            }
         }
         catch (Exception e)
         {
            
         }
      }

      private async Task DisconnectFromBroker()
      {
         await WriteOutboundControlFrame("DISCONNECTING");
         _client.DisconnectedAsync -= ClientOnDisconnectedAsync;
         await _client.DisconnectAsync();
         await WriteOutboundControlFrame("DISCONNECTED");
      }

      private async Task ClientOnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
      {
         await WriteOutboundControlFrame("DISCONNECTED");
         await Task.Delay(_serviceOptions.ReconnectInterval);
         await ConnectToBroker();
      }
      
      private async Task OnClientOnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
      {
         var payloadString = arg.ApplicationMessage.ConvertPayloadToString();
         await WriteOutboundMessageFrame(new { arg.ApplicationMessage.Topic, payloadString });
      }
   }
   
/*_task1 = Task.Run(async () =>
{
   try
   {
      while (await _channelReader.WaitToReadAsync(_token1))
      {
         await foreach (var frame in _channelReader.ReadAllAsync(_token1))
         {
            await ProcessInboundFrame(frame);
         }
      }
   }
   catch (OperationCanceledException ocex)
   {
      Console.WriteLine("MQTT.ClientService MQTT.CHANNEL_READER Cancelled");
   }
   catch (Exception ex)
   {
      Console.WriteLine("MQTT.ClientService MQTT.CHANNEL_READER ERROR");
      Console.WriteLine(ex);
   }
   finally
   {
      Console.WriteLine("MQTT.ClientService MQTT.CHANNEL_READER Stopping");
      _appLifetime.StopApplication();
   }
}, _token1);
*/