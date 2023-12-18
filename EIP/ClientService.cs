using System.Threading.Channels;
using libplctag.DataTypes.Simple;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Derail.EIP;

public class ClientService: IHostedService
   {
      private readonly string _instanceId;
      private readonly ClientServiceOptions _serviceOptions;
      private readonly ILogger<ClientService> _logger;
      private readonly IHostApplicationLifetime _appLifetime;
      private ChannelWriter<SystemControlFrame> _channelControlWriter;
      private ChannelWriter<SystemMessageFrame> _channelMessageWriter;

      private List<libplctag.ITag> _tagCache = new();
      
      private Task _task1;
      private CancellationTokenSource _tokenSource1;
      private CancellationToken _token1;
      
      public ClientService(
         string instanceId,
         IOptionsMonitor<ClientServiceOptions> optionsMonitor,
         ILogger<ClientService> logger,
         IHostApplicationLifetime appLifetime,
         ChannelWriter<SystemControlFrame> channelSystemWriter,
         ChannelWriter<SystemMessageFrame> channelMessageWriter)
      {
         _instanceId = instanceId;
         _serviceOptions = optionsMonitor.Get(instanceId);
         _logger = logger;
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
                  if (_serviceOptions.Enabled)
                  {
                     CreateTags();
                  }

                  while (!_token1.IsCancellationRequested)
                  {
                     if (_serviceOptions.Enabled)
                     {
                        await ReadTagsAsync();
                     }
                     
                     await Task.Delay(_serviceOptions.ReadInterval);
                  }
                  
                  if (_serviceOptions.Enabled)
                  {
                     DestroyTags();
                  }

               }
               catch (Exception ex)
               {
                  _logger.LogError(ex, "CLIENT ERROR");
                  
                  await WriteOutboundControlFrame("ERROR", ex);
               }
               finally
               {
                  _logger.LogInformation("CLIENT Stopping");
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

      private async Task WriteOutboundControlFrame(string @event, dynamic? data = null)
      {
         _logger.LogDebug($"Write Frame: CONTROL/{@event}");
         await _channelControlWriter.WriteAsync(new SystemControlFrame()
         {
            SourceInstanceId = _instanceId,
            Payload = new { @event, data }
         });
      }
      
      private async Task WriteOutboundMessageFrame(dynamic payload)
      {
         _logger.LogDebug($"Write Frame: MESSAGE");
         await _channelMessageWriter.WriteAsync(new SystemMessageFrame()
         {
            SourceInstanceId = _instanceId,
            Payload = payload
         });
      }

      private void CreateTags()
      {
         foreach (var tag in _serviceOptions.Tags)
         {
            Type mapperType = Type.GetType($"libplctag.DataTypes.Simple.{tag.Mapper}, libplctag");
            
            if (mapperType == null)
            {
               _logger.LogWarning($"Tag mapper class '{tag.Mapper}' not found for tag '{tag.Name}'.");
            }
            else
            {
               var mapperInstance = Activator.CreateInstance(mapperType) as libplctag.ITag;
               if (mapperInstance == null)
               {
                  _logger.LogWarning($"Failed to activate tag mapper class '{tag.Mapper}' for tag '{tag.Name}'.");
                  continue;
               }
               
               mapperInstance.Name = tag.Name;
               mapperInstance.Gateway = _serviceOptions.Gateway;
               mapperInstance.Path = _serviceOptions.Path;
               mapperInstance.PlcType = _serviceOptions.PlcType;
               mapperInstance.Protocol = _serviceOptions.Protocol;
               mapperInstance.Timeout = _serviceOptions.Timeout;
               _tagCache.Add(mapperInstance);
            }
         }
      }
      
      private async Task ReadTagsAsync()
      {
         foreach (var tag in _tagCache)
         {
            // TODO: try-catch
            var tagResponse = await tag.ReadAsync();
            await WriteOutboundMessageFrame(new { Tag = tag.Name, Value = tagResponse });
         }
      }

      private void DestroyTags()
      {
         foreach (var tag in _tagCache)
         {
            tag.Dispose();
         }
      }
   }
   