using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Derail.DeviceCache;

public class ClientService: IHostedService
   {
      private readonly string _instanceId;
      private readonly IHostApplicationLifetime _appLifetime;
      private readonly ILogger<ClientService> _logger;
      private ChannelReader<SystemControlFrame> _channelControlReader;
      private ChannelReader<SystemMessageFrame> _channelMessageReader;
      
      private Task _task1;
      private CancellationTokenSource _tokenSource1;
      private CancellationToken _token1;
      
      private Task _task2;
      private CancellationTokenSource _tokenSource2;
      private CancellationToken _token2;
      
      public ClientService(
         string instanceId,
         ILogger<ClientService> logger,
         IHostApplicationLifetime appLifetime,
         ChannelReader<SystemControlFrame> channelControlReader,
         ChannelReader<SystemMessageFrame> channelMessageReader)
      {
         _instanceId = instanceId;
         _logger = logger;
         _appLifetime = appLifetime;
         _channelControlReader = channelControlReader;
         _channelMessageReader = channelMessageReader;
      }
      
      public Task StartAsync(CancellationToken cancellationToken)
      {
         _appLifetime.ApplicationStarted.Register(() =>
         {
            _tokenSource1 = new CancellationTokenSource();
            _token1 = _tokenSource1.Token;
            
            _tokenSource2 = new CancellationTokenSource();
            _token2 = _tokenSource1.Token;
            
            _task1 = Task.Run(async () =>
            {
               try
               {
                  while (await _channelMessageReader.WaitToReadAsync(_token1))
                  {
                     await foreach (var frame in _channelMessageReader.ReadAllAsync(_token1))
                     {
                        await ProcessInboundMessageFrame(frame);
                     }
                  }
               }
               catch (OperationCanceledException ocex)
               {
                  _logger.LogWarning("MESSAGE_CHANNEL_READER Cancelled");
               }
               catch (Exception ex)
               {
                  _logger.LogError(ex, "MESSAGE_CHANNEL_READER ERROR");
               }
               finally
               {
                  _logger.LogInformation("MESSAGE_CHANNEL_READER Stopping");
                  _appLifetime.StopApplication();
               }
            }, _token1);
            
            _task2 = Task.Run(async () =>
            {
               try
               {
                  while (await _channelControlReader.WaitToReadAsync(_token2))
                  {
                     await foreach (var frame in _channelControlReader.ReadAllAsync(_token2))
                     {
                        await ProcessInboundControlFrame(frame);
                     }
                  }
               }
               catch (OperationCanceledException ocex)
               {
                  _logger.LogWarning("CONTROL_CHANNEL_READER Cancelled");
               }
               catch (Exception ex)
               {
                  _logger.LogError(ex, "CONTROL_CHANNEL_READER ERROR");
               }
               finally
               {
                  _logger.LogInformation("CONTROL_CHANNEL_READER Stopping");
                  _appLifetime.StopApplication();
               }
            }, _token2);
         });

         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("Stop");
         
         _tokenSource1.Cancel();
         _tokenSource2.Cancel();
         Task.WaitAll(_task1, _task2);
         
         return Task.CompletedTask;
      }

      private async Task ProcessInboundMessageFrame(SystemMessageFrame frame)
      { 
         _logger.LogDebug($"Receive Message Frame: [{frame.TimestampCreated},{frame.SourceInstanceId}] {frame.Payload}");
      }
      
      private async Task ProcessInboundControlFrame(SystemControlFrame frame)
      { 
         _logger.LogDebug($"Receive Control Frame: [{frame.TimestampCreated},{frame.SourceInstanceId}] {frame.Payload}");
      }
   }