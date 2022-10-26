using System;

namespace Digithought.Framework
{
    public interface IWorkerQueue
    {
        int Count { get; }
        void Clear();
        bool CurrentThreadOn();
        void Execute(Action action);
        void Queue(Action action);
        void Wait();
    }
}