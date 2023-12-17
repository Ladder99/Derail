using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace Derail.DeviceCache;

public class ClientService: IHostedService
   {
      private readonly IHostApplicationLifetime _appLifetime;
      private ChannelReader<SystemMessageFrame> _channelMessageReader;
      
      private Task _task1;
      private CancellationTokenSource _tokenSource1;
      private CancellationToken _token1;
      
      public ClientService(
         IHostApplicationLifetime appLifetime,
         ChannelReader<SystemMessageFrame> channelMessageReader)
      {
         _appLifetime = appLifetime;
         _channelMessageReader = channelMessageReader;
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
                  while (await _channelMessageReader.WaitToReadAsync(_token1))
                  {
                     await foreach (var frame in _channelMessageReader.ReadAllAsync(_token1))
                     {
                        await ProcessInboundFrame(frame);
                     }
                  }
               }
               catch (OperationCanceledException ocex)
               {
                  Console.WriteLine("DeviceService.ClientService MQTT.CHANNEL_READER Cancelled");
               }
               catch (Exception ex)
               {
                  Console.WriteLine("DeviceService.ClientService MQTT.CHANNEL_READER ERROR");
                  Console.WriteLine(ex);
               }
               finally
               {
                  Console.WriteLine("DeviceService.ClientService MQTT.CHANNEL_READER Stopping");
                  _appLifetime.StopApplication();
               }
            }, _token1);
         });

         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         Console.WriteLine("DeviceService.ClientService Stop");
         
         _tokenSource1.Cancel();
         Task.WaitAll(_task1);
         
         return Task.CompletedTask;
      }

      private async Task ProcessInboundFrame(SystemMessageFrame frame)
      {
         Console.WriteLine($"DeviceService Receive Frame: {frame.SourceInstanceId} = {frame.Payload}");

      }
   }