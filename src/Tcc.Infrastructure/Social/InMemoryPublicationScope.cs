using Tcc.Application.Social;

namespace Tcc.Infrastructure.Social;

public sealed class InMemoryPublicationScope : IPublicationScope
{
    internal Lock Gate { get; } = new();
    private readonly Stack<List<Action>> frames = new();
    internal void OnRollback(Action undo)
    {
        if (frames.Count > 0) frames.Peek().Add(undo);
    }
    public T Execute<T>(Func<T> action)
    {
        lock (Gate)
        {
            var undo = new List<Action>();
            frames.Push(undo);
            try
            {
                var result = action();
                frames.Pop();
                if (frames.Count > 0) frames.Peek().AddRange(undo);
                return result;
            }
            catch
            {
                frames.Pop();
                for (var i = undo.Count - 1; i >= 0; i--) undo[i]();
                throw;
            }
        }
    }
}
