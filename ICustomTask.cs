namespace CustomThreadPool
{
    public interface ICustomTask
    {
        Task ExecuteAsync();
        string Name { get; }
    }
}