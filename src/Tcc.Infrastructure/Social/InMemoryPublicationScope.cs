using Tcc.Application.Social;
namespace Tcc.Infrastructure.Social;
public sealed class InMemoryPublicationScope : IPublicationScope
{
    internal Lock Gate { get; } = new();
    private sealed class Frame { public List<Action> Undo { get; } = []; public List<Action> Pending { get; } = []; }
    private readonly Stack<Frame> frames = new();
    internal void OnRollback(Action undo) { if (frames.Count > 0) frames.Peek().Undo.Add(undo); }
    internal void Stage(Action apply) { if (frames.Count > 0) frames.Peek().Pending.Add(apply); else apply(); }
    private static void Undo(Frame frame) { for (var i = frame.Undo.Count - 1; i >= 0; i--) frame.Undo[i](); }
    public T Execute<T>(Func<T> action)
    {
        lock (Gate)
        {
            var frame = new Frame(); frames.Push(frame);
            try
            {
                var result = action();
                foreach (var apply in frame.Pending) apply();
                frames.Pop();
                if (frames.Count > 0) frames.Peek().Undo.AddRange(frame.Undo);
                return result;
            }
            catch { frames.Pop(); Undo(frame); throw; }
        }
    }
    public IPreparedPublication Prepare(Action action)
    {
        lock (Gate)
        {
            var frame = new Frame(); frames.Push(frame);
            try { action(); frames.Pop(); return new Prepared(this, frame); }
            catch { frames.Pop(); Undo(frame); throw; }
        }
    }
    private sealed class Prepared(InMemoryPublicationScope owner, Frame frame) : IPreparedPublication
    {
        private bool complete;
        public void Commit()
        {
            lock (owner.Gate)
            {
                if (complete) return;
                owner.frames.Push(frame);
                try { foreach (var apply in frame.Pending) apply(); owner.frames.Pop(); complete = true; }
                catch { owner.frames.Pop(); Undo(frame); complete = true; throw; }
            }
        }
        public void Dispose() { lock (owner.Gate) { if (!complete) { Undo(frame); complete = true; } } }
    }
}