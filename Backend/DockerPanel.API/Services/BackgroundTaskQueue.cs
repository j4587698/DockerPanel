using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services
{
    /// <summary>
    /// 后台任务队列服务
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue, IHostedService
    {
        private readonly Channel<Func<CancellationToken, Task>> _queue;
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _backgroundTask;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();

            // 创建有界通道以防止内存过度使用
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
        }

        /// <summary>
        /// 将后台工作项加入队列
        /// </summary>
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            try
            {
                if (!_queue.Writer.TryWrite(workItem))
                {
                    _logger.LogWarning("无法将后台工作项加入队列，队列可能已满");
                }
                else
                {
                    _logger.LogInformation("后台任务已成功加入队列，当前队列实例: {HashCode}", GetHashCode());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "将后台工作项加入队列时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 从队列中取出后台工作项
        /// </summary>
        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);
            return workItem;
        }

        /// <summary>
        /// 启动后台服务
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("启动后台任务队列服务");
            _backgroundTask = Task.Run(BackgroundProcessing, _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止后台服务
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("停止后台任务队列服务");

            // 取消后台处理
            _cancellationTokenSource.Cancel();

            // 等待队列完成处理
            _queue.Writer.Complete();

            try
            {
                if (_backgroundTask != null)
                {
                    await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
                }
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// 后台处理循环
        /// </summary>
        private async Task BackgroundProcessing()
        {
            _logger.LogInformation("后台任务处理循环已启动");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("等待从队列读取任务，队列实例: {HashCode}", GetHashCode());
                    var workItem = await _queue.Reader.ReadAsync(_cancellationTokenSource.Token);
                    _logger.LogInformation("从队列读取到任务，准备执行");

                    if (workItem != null)
                    {
                        _logger.LogInformation("开始执行后台任务");
                        await workItem(_cancellationTokenSource.Token);
                        _logger.LogInformation("后台任务执行完成");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("后台任务处理被取消");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "执行后台任务时发生错误");
                }
            }

            _logger.LogInformation("后台任务处理循环已停止");
        }
    }

    /// <summary>
    /// 后台任务队列接口
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// 将后台工作项加入队列
        /// </summary>
        /// <param name="workItem">要执行的工作项</param>
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        /// <summary>
        /// 从队列中取出后台工作项
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工作项</returns>
        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 后台任务服务扩展
    /// </summary>
    public static class BackgroundTaskQueueExtensions
    {
        /// <summary>
        /// 添加后台任务队列服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services)
        {
            // 🔧 修复：确保 IBackgroundTaskQueue 和 IHostedService 使用同一个实例
            // 先注册为单例
            services.AddSingleton<BackgroundTaskQueue>();

            // 接口指向同一个实例
            services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());

            // 托管服务也使用同一个实例
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<BackgroundTaskQueue>());

            return services;
        }
    }
}