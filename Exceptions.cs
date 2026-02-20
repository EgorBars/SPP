using System;

namespace TestingEngine
{
    public class EngineException : Exception
    {
        public EngineException(string msg) : base(msg) { }
    }
}
