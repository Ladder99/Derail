using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Derail.DeviceCache;

public class ClientService: IHostedService
   {
      private readonly IHostApplicationLifetime _appLifetime;
      private readonly ILogger<ClientService> _logger;
      private ChannelReader<SystemMessageFrame> _channelMessageReader;
      
      private Task _task1;
      private CancellationTokenSource _tokenSource1;
      private CancellationToken _token1;
      
      public ClientService(
         IHostApplicationLifetime appLifetime,
         ILogger<ClientService> logger,
         ChannelReader<SystemMessageFrame> channelMessageReader)
      {
         _appLifetime = appLifetime;
         _logger = logger;
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
                  _logger.LogWarning("CHANNEL_READER Cancelled");
               }
               catch (Exception ex)
               {
                  _logger.LogError(ex, "CHANNEL_READER ERROR");
               }
               finally
               {
                  _logger.LogInformation("CHANNEL_READER Stopping");
                  _appLifetime.StopApplication();
               }
            }, _token1);
         });

         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("Stop");
         
         _tokenSource1.Cancel();
         Task.WaitAll(_task1);
         
         return Task.CompletedTask;
      }

      private async Task ProcessInboundFrame(SystemMessageFrame frame)
      { 
         Console.WriteLine($"DeviceService Receive Frame: [{frame.TimestampCreated},{frame.SourceInstanceId}] {frame.Payload}");
         
        /*
         var client = new UdpClient();
         IPEndPoint ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9870);
         client.Connect(ep);
         //var ps = Encoding.UTF8.GetString(frame.Payload.PayloadSegment);
         //var jj = JsonConvert.SerializeObject(frame.Payload.PayloadSegment);
         //Console.WriteLine(ps);
         await client.SendAsync(frame.Payload.PayloadSegment.Array);
         */
      }
   }