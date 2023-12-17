namespace Derail;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class TerminatorService : IHostedService
{
    public class TerminatorServiceOptions
    {
        public int TerminateInMs
        {
            get;
            set;
        }
    }
    
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly TerminatorServiceOptions _options;
    
    private ChannelReader<bool> _tsInChannelReader;
      
    private Task _task1;
    private CancellationTokenSource _tokenSource1;
    private CancellationToken _token1;
    
    public TerminatorService(
        IHostApplicationLifetime appLifetime,
        TerminatorServiceOptions options,
        ChannelReader<bool> tsInChannelReader)
    {
        _appLifetime = appLifetime;
        _options = options;
        _tsInChannelReader = tsInChannelReader;
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
                    if (_options.TerminateInMs > 0)
                    {
                        await Task.Delay(_options.TerminateInMs);
                    }
                    else
                    {
                        while (await _tsInChannelReader.WaitToReadAsync(_token1))
                        {
                            await foreach (var frame in _tsInChannelReader.ReadAllAsync(_token1))
                            {
                                await Task.Yield();
                            }
                        }
                    }
                }
                catch (OperationCanceledException ocex)
                {
                    Console.WriteLine("TerminatorService CHANNEL_READER Cancelled");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TerminatorService CHANNEL_READER ERROR");
                    Console.WriteLine(ex);
                }
                finally
                {
                    Console.WriteLine("TerminatorService CHANNEL_READER Stopping");
                    _appLifetime.StopApplication();
                }
            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("TerminatorService Stop");
        
        _tokenSource1.Cancel();
        Task.WaitAll(_task1);
        
        return Task.CompletedTask;
    }
}