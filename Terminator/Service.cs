using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Derail.Terminator;

public class Service : IHostedService
{
    private readonly string _instanceId;
    private readonly ServiceOptions _serviceOptions;
    private readonly ILogger<Service> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private ChannelReader<bool> _terminatorChannelReader;
      
    private Task _task1;
    private CancellationTokenSource _tokenSource1;
    private CancellationToken _token1;
    
    public Service(
        string instanceId,
        IOptionsMonitor<ServiceOptions> optionsMonitor,
        ILogger<Service> logger,
        IHostApplicationLifetime appLifetime,
        ChannelReader<bool> terminatorChannelReader)
    {
        _instanceId = instanceId;
        _serviceOptions = optionsMonitor.Get(instanceId);
        _logger = logger;
        _appLifetime = appLifetime;
        _terminatorChannelReader = terminatorChannelReader;
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
                    if (_serviceOptions.TerminateInMs > 0)
                    {
                        await Task.Delay(_serviceOptions.TerminateInMs);
                    }
                    else
                    {
                        while (await _terminatorChannelReader.WaitToReadAsync(_token1))
                        {
                            await foreach (var frame in _terminatorChannelReader.ReadAllAsync(_token1))
                            {
                                await Task.Yield();
                            }
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
            });
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
}