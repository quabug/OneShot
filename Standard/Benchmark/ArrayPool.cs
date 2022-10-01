using System.Collections.Concurrent;

namespace Benchmark;

struct ArrayPool<T> : IDisposable
{
    private static volatile ConcurrentStack<T[]>[] _pool;
    private static object _expandLock = new object();
    public T[] Value { get; private set; }
    
    public static ArrayPool<T> Rent(int size)
    {
        if (size == 0) return new ArrayPool<T> { Value = Array.Empty<T>() };
        if (_pool.Length <= size)
        {
            lock (_expandLock)
            {
                if (_pool.Length <= size)
                    _pool = Expand(_pool, size);
            }
        }

        var stack = _pool[size];
        if (!stack.TryPop(out var value)) value = new T[size];
        return new ArrayPool<T> { Value = value };
    }

    private static ConcurrentStack<T[]>[] Expand(ConcurrentStack<T[]>[]? pool,  int length)
    {
        var size = pool?.Length ?? 0;
        Array.Resize(ref pool, length);
        for (var i = size; i < length; i++) pool[i] = new ConcurrentStack<T[]>();
        return pool;
    }

    public void Dispose()
    {
        if (Value.Length == 0) return;
        _pool[Value.Length].Push(Value);
    }
}
