using System;

namespace TestingEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SuiteAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class CaseAttribute : Attribute
    {
        public string Title { get; set; }
        public int Order { get; set; } = 0;
        public CaseAttribute(string title = "") => Title = title;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SkipAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CaseDataAttribute : Attribute
    {
        public object[] Args { get; }
        public CaseDataAttribute(params object[] args) => Args = args;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class StartupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class CleanupAttribute : Attribute { }
}