using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CustomThreadPool
{
    public class DynamicThreadPool : IDisposable
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly TimeSpan _threadIdleTimeout;
        private readonly TimeSpan _queueWaitTimeout;

        private readonly BlockingCollection<ICustomTask> _taskQueue;
        private readonly List<PoolThread> _threads;
        private readonly object _threadLock = new object();
        private readonly CancellationTokenSource _cts;

        private int _activeThreads;
        private bool _disposed;

        // Мониторинг
        private readonly Stopwatch _monitoringTimer;
        private DateTime _lastLogTime;

        public DynamicThreadPool(
            int minThreads = 2,
            int maxThreads = 10,
            int threadIdleTimeoutSeconds = 30,
            int queueWaitTimeoutMilliseconds = 5000)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _threadIdleTimeout = TimeSpan.FromSeconds(threadIdleTimeoutSeconds);
            _queueWaitTimeout = TimeSpan.FromMilliseconds(queueWaitTimeoutMilliseconds);

            _taskQueue = new BlockingCollection<ICustomTask>(new ConcurrentQueue<ICustomTask>());
            _threads = new List<PoolThread>();
            _cts = new CancellationTokenSource();
            _monitoringTimer = Stopwatch.StartNew();
            _lastLogTime = DateTime.Now;

            // Создаем начальное количество потоков
            for (int i = 0; i < _minThreads; i++)
            {
                CreateThread();
            }

            // Запускаем мониторинг масштабирования
            Task.Run(ScalingMonitor);
        }

        private void CreateThread()
        {
            lock (_threadLock)
            {
                if (_threads.Count >= _maxThreads)
                    return;

                var thread = new PoolThread(_taskQueue, _cts.Token, _threadIdleTimeout, _queueWaitTimeout);
                _threads.Add(thread);
                thread.Start();
                _activeThreads++;

                Log($"Создан поток #{thread.Id}, всего потоков: {_threads.Count}");
            }
        }

        private void RemoveThread(PoolThread thread)
        {
            lock (_threadLock)
            {
                if (_threads.Remove(thread))
                {
                    _activeThreads--;
                    Log($"Удален поток #{thread.Id}, осталось потоков: {_threads.Count}");
                }
            }
        }

        private async Task ScalingMonitor()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000); // Проверяем каждую секунду

                var queueSize = _taskQueue.Count;
                var currentThreads = _threads.Count;

                // Масштабирование вверх: если очередь большая и есть место для новых потоков
                if (queueSize > currentThreads * 2 && currentThreads < _maxThreads)
                {
                    int threadsToAdd = Math.Min(_maxThreads - currentThreads, queueSize - currentThreads);
                    for (int i = 0; i < threadsToAdd; i++)
                    {
                        CreateThread();
                    }
                    Log($"Масштабирование вверх: добавлено {threadsToAdd} потоков. Очередь: {queueSize}");
                }

                // Логируем состояние каждые 5 секунд
                if (DateTime.Now - _lastLogTime > TimeSpan.FromSeconds(5))
                {
                    Log($"Состояние: Потоков: {currentThreads}, Активных: {_activeThreads}, Очередь: {queueSize}");
                    _lastLogTime = DateTime.Now;
                }
            }
        }

        public void EnqueueTask(ICustomTask task)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DynamicThreadPool));

            _taskQueue.Add(task);
            Log($"Добавлена задача: {task.Name}, размер очереди: {_taskQueue.Count}");
        }

        public async Task WaitForCompletionAsync()
        {
            // Ждем, пока очередь не опустеет и все задачи не завершатся
            while (_taskQueue.Count > 0 || _activeThreads > 0)
            {
                await Task.Delay(100);
            }
        }

        private void Log(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ThreadPool] {message}");
                Console.ResetColor();
            }
        }

        private static readonly object _consoleLock = new object();

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();
            _taskQueue.CompleteAdding();

            foreach (var thread in _threads)
            {
                thread.Join(TimeSpan.FromSeconds(5));
            }

            _cts.Dispose();
            _taskQueue.Dispose();
            _disposed = true;
        }
    }

    public class PoolThread
    {
        public int Id { get; }
        private readonly BlockingCollection<ICustomTask> _taskQueue;
        private readonly CancellationToken _cancellationToken;
        private readonly TimeSpan _idleTimeout;
        private readonly TimeSpan _queueWaitTimeout;
        private readonly Thread _thread;
        private DateTime _lastActivity;
        private bool _isRunning;

        public PoolThread(
            BlockingCollection<ICustomTask> taskQueue,
            CancellationToken cancellationToken,
            TimeSpan idleTimeout,
            TimeSpan queueWaitTimeout)
        {
            Id = Thread.CurrentThread.ManagedThreadId;
            _taskQueue = taskQueue;
            _cancellationToken = cancellationToken;
            _idleTimeout = idleTimeout;
            _queueWaitTimeout = queueWaitTimeout;
            _thread = new Thread(ExecuteLoop) { IsBackground = true };
            _lastActivity = DateTime.Now;
        }

        public void Start()
        {
            _thread.Start();
        }

        private async void ExecuteLoop()
        {
            _isRunning = true;

            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Пытаемся получить задачу с таймаутом
                    if (_taskQueue.TryTake(out var task, _queueWaitTimeout, _cancellationToken))
                    {
                        _lastActivity = DateTime.Now;
                        await ExecuteTaskWithErrorHandling(task);
                    }
                    else
                    {
                        // Проверяем, не простаивает ли поток слишком долго
                        if (DateTime.Now - _lastActivity > _idleTimeout)
                        {
                            Log($"Поток #{Id} завершается из-за простоя");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Ошибка в потоке #{Id}: {ex.Message}");
                    // Продолжаем работу, не падаем
                }
            }

            _isRunning = false;
        }

        private async Task ExecuteTaskWithErrorHandling(ICustomTask task)
        {
            try
            {
                Log($"Поток #{Id} выполняет задачу: {task.Name}");
                await task.ExecuteAsync();
                Log($"Поток #{Id} завершил задачу: {task.Name}");
            }
            catch (Exception ex)
            {
                Log($"Поток #{Id} ошибка при выполнении {task.Name}: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            lock (DynamicThreadPool._consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                Console.ResetColor();
            }
        }

        public void Join(TimeSpan timeout)
        {
            _thread.Join(timeout);
        }
    }
}