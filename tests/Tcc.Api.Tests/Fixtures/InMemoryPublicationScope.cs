namespace Tcc.Application.Social
{
    public interface IPublicationScope { T Execute<T>(Func<T> action); }
}
namespace Tcc.Infrastructure.Social
{
    public sealed class InMemoryPublicationScope : Tcc.Application.Social.IPublicationScope
    {
        internal Lock Gate { get; } = new();
        private readonly SemaphoreSlim writers = new(1, 1);
        private readonly AsyncLocal<List<Action>?> undo = new();
        internal void OnRollback(Action action) => undo.Value?.Add(action);
        public T Execute<T>(Func<T> action) { lock (Gate) return action(); }
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct)
        {
            await writers.WaitAsync(ct);
            undo.Value = [];
            try { return await action(); }
            catch { lock (Gate) { for (var i = undo.Value.Count - 1; i >= 0; i--) undo.Value[i](); } throw; }
            finally { undo.Value = null; writers.Release(); }
        }
    }
}
