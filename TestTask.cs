using System;
using System.Reflection;
using System.Threading.Tasks;
using TestingEngine;

namespace CustomThreadPool
{
    public class TestTask : ICustomTask
    {
        public string Name { get; }
        private readonly Type _type;
        private readonly MethodInfo _method;
        private readonly object[] _args;
        private readonly Func<Task<string>> _executeFunc;

        public TestTask(Type type, MethodInfo method, object[] args)
        {
            _type = type;
            _method = method;
            _args = args;
            Name = $"{method.Name}({string.Join(",", args ?? new object[0])})";
            _executeFunc = () => ExecuteTestAsync();
        }

        private async Task<string> ExecuteTestAsync()
        {
            // Копируем логику из ExecuteSingleTest
            var instance = Activator.CreateInstance(_type);
            var startup = _type.GetMethod("Init");
            var cleanup = _type.GetMethod("End");

            try
            {
                startup?.Invoke(instance, null);

                // Конвертация аргументов
                var parameters = _method.GetParameters();
                var convertedArgs = _args?.Select((a, i) =>
                    Convert.ChangeType(a, parameters[i].ParameterType)).ToArray();

                var result = _method.Invoke(instance, convertedArgs);
                if (result is Task task)
                    await task;

                return "PASSED";
            }
            catch (Exception ex)
            {
                return $"FAILED: {ex.InnerException?.Message ?? ex.Message}";
            }
            finally
            {
                cleanup?.Invoke(instance, null);
            }
        }

        public async Task ExecuteAsync()
        {
            await _executeFunc();
        }
    }
}