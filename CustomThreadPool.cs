using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestingEngine;

namespace CustomThreadPool
{
    // ==================== ИНТЕРФЕЙС ЗАДАЧИ ====================
    public interface ICustomTask
    {
        Task ExecuteAsync();
        string Name { get; }
    }

    // ==================== АДАПТЕР ДЛЯ ТЕСТОВ ====================
    public class TestTask : ICustomTask
    {
        public string Name { get; }

        private readonly Type _testClassType;
        private readonly MethodInfo _testMethod;
        private readonly object[] _testArguments;

        public TestTask(Type testClassType, MethodInfo testMethod, object[] testArguments)
        {
            _testClassType = testClassType;
            _testMethod = testMethod;
            _testArguments = testArguments;

            string argsStr = testArguments != null ? string.Join(",", testArguments) : "";
            Name = $"{testMethod.Name}({argsStr})";
        }

        public async Task ExecuteAsync()
        {
            var testInstance = Activator.CreateInstance(_testClassType);
            var startupMethod = _testClassType.GetMethod("Init");
            var cleanupMethod = _testClassType.GetMethod("End");

            try
            {
                startupMethod?.Invoke(testInstance, null);

                var parameters = _testMethod.GetParameters();
                object[] convertedArgs = null;

                if (_testArguments != null && parameters.Length > 0)
                {
                    convertedArgs = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        convertedArgs[i] = Convert.ChangeType(_testArguments[i], parameters[i].ParameterType);
                    }
                }

                var result = _testMethod.Invoke(testInstance, convertedArgs);

                if (result is Task task)
                {
                    await task;
                }
            }
            finally
            {
                cleanupMethod?.Invoke(testInstance, null);
            }
        }
    }

    // ==================== РАБОЧИЙ ПОТОК ====================
    public class PoolThread
    {
        public int Id { get; private set; }

        private readonly BlockingCollection<ICustomTask> _taskQueue;
        private readonly CancellationToken _cancellationToken;
        private readonly TimeSpan _idleTimeout;
        private readonly TimeSpan _queueWaitTimeout;
        private readonly Thread _thread;
        private DateTime _lastActivity;

        private static readonly object _consoleLock = new object();

        public PoolThread(
            BlockingCollection<ICustomTask> taskQueue,
            CancellationToken cancellationToken,
            TimeSpan idleTimeout,
            TimeSpan queueWaitTimeout)
        {
            _taskQueue = taskQueue;
            _cancellationToken = cancellationToken;
            _idleTimeout = idleTimeout;
            _queueWaitTimeout = queueWaitTimeout;

            _thread = new Thread(ExecuteLoop);
            _thread.IsBackground = true;
            _lastActivity = DateTime.Now;
        }

        public void Start()
        {
            _thread.Start();
            Id = _thread.ManagedThreadId;
            Log($"Поток запущен");
        }

        private async void ExecuteLoop()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool gotTask = _taskQueue.TryTake(out var task, _queueWaitTimeout, _cancellationToken);

                    if (gotTask && task != null)
                    {
                        _lastActivity = DateTime.Now;
                        Log($"▶ Выполняю: {task.Name}");

                        try
                        {
                            await task.ExecuteAsync();
                            Log($"✓ Завершил: {task.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log($"✗ Ошибка: {ex.Message}");
                        }
                    }
                    else
                    {
                        var idleTime = DateTime.Now - _lastActivity;
                        if (idleTime > _idleTimeout)
                        {
                            Log($"⏹ Завершение (простой {idleTime.TotalSeconds:F1}с)");
                            break;
                        }

                        await Task.Delay(100);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"⚠ Ошибка в цикле: {ex.Message}");
                }
            }

            Log($"Поток остановлен");
        }

        private void Log(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Поток #{Id}] {message}");
                Console.ResetColor();
            }
        }

        public void Join(TimeSpan timeout)
        {
            _thread.Join(timeout);
        }

        public bool IsAlive => _thread.IsAlive;
    }

    // ==================== ДИНАМИЧЕСКИЙ ПУЛ ПОТОКОВ ====================
    public class DynamicThreadPool : IDisposable
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly TimeSpan _threadIdleTimeout;
        private readonly TimeSpan _queueWaitTimeout;

        private readonly BlockingCollection<ICustomTask> _taskQueue;
        private readonly List<PoolThread> _threads;
        private readonly object _threadsLock = new object();
        private readonly CancellationTokenSource _cts;

        private bool _disposed;
        private Timer _monitoringTimer;

        private static readonly object _consoleLock = new object();

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

            Log($"🚀 Пул создан | Min: {_minThreads} | Max: {_maxThreads} | Idle: {_threadIdleTimeout.TotalSeconds}с");

            for (int i = 0; i < _minThreads; i++)
            {
                AddThread();
            }

            StartMonitoring();
        }

        private void AddThread()
        {
            lock (_threadsLock)
            {
                if (_threads.Count >= _maxThreads)
                    return;

                var thread = new PoolThread(_taskQueue, _cts.Token, _threadIdleTimeout, _queueWaitTimeout);
                _threads.Add(thread);
                thread.Start();

                Log($"➕ Добавлен поток | Всего: {_threads.Count}/{_maxThreads}");
            }
        }

        private void RemoveDeadThreads()
        {
            lock (_threadsLock)
            {
                var deadThreads = _threads.Where(t => !t.IsAlive).ToList();
                foreach (var thread in deadThreads)
                {
                    _threads.Remove(thread);
                    Log($"➖ Удален поток | Осталось: {_threads.Count}");
                }
            }
        }

        private void StartMonitoring()
        {
            _monitoringTimer = new Timer(_ =>
            {
                if (_disposed) return;

                int queueSize = _taskQueue.Count;
                int currentThreads = _threads.Count;

                // Масштабирование вверх
                if (queueSize > currentThreads && currentThreads < _maxThreads)
                {
                    int threadsToAdd = Math.Min(_maxThreads - currentThreads, Math.Max(1, queueSize - currentThreads));
                    Log($"📈 Масштабирование | Очередь: {queueSize} | Потоков: {currentThreads} | Добавляем: {threadsToAdd}");

                    for (int i = 0; i < threadsToAdd; i++)
                    {
                        AddThread();
                    }
                }

                RemoveDeadThreads();

                // Логируем состояние каждые 5 секунд
                if (DateTime.Now.Second % 5 == 0)
                {
                    Log($"📊 Статус | Потоков: {_threads.Count} | Очередь: {queueSize}");
                }

            }, null, 2000, 2000);
        }

        public void EnqueueTask(ICustomTask task)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DynamicThreadPool));

            _taskQueue.Add(task);
            Log($"📝 Добавлена: {task.Name} | Очередь: {_taskQueue.Count}");
        }

        public async Task WaitForCompletionAsync()
        {
            Log("⏳ Ожидание завершения всех задач...");

            while (_taskQueue.Count > 0)
            {
                await Task.Delay(100);
            }

            await Task.Delay(500);
            Log("✅ Все задачи выполнены");
        }

        public int GetActiveThreadCount()
        {
            lock (_threadsLock)
            {
                return _threads.Count;
            }
        }

        private void Log(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ПУЛ] {message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Log("🛑 Остановка пула...");

            _cts.Cancel();
            _taskQueue.CompleteAdding();

            _monitoringTimer?.Dispose();

            lock (_threadsLock)
            {
                foreach (var thread in _threads)
                {
                    thread.Join(TimeSpan.FromSeconds(3));
                }
                _threads.Clear();
            }

            _cts.Dispose();
            _taskQueue.Dispose();

            _disposed = true;
            Log("Пул остановлен");
        }
    }
}