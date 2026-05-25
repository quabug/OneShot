using System.Diagnostics;

namespace OneShot.Test;

public class TestMultiThread
{
    long _threadCount = 100;

    // Stress-test fixtures. The list pulls double duty: the worker thread picks
    // a random Type and registers it via the non-generic Container.Register(Type)
    // overload, but that overload requires generated ITypeInfo in the registry.
    // The SourceGenManifest method below is what anchors the source generator -
    // it's never called, just scanned syntactically for Register<T>() call sites.
    private static readonly Type[] s_types =
    [
        typeof(A), typeof(B), typeof(C), typeof(D),
        typeof(E), typeof(F), typeof(G), typeof(H),
        typeof(AB), typeof(ABC), typeof(ABCD),
        typeof(BCD), typeof(FG), typeof(CDEF),
    ];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051",
        Justification = "Never invoked; the source generator scans the Register<T>() calls to emit ITypeInfo for these fixtures.")]
    private static void SourceGenManifest(Container c)
    {
        c.Register<A>(); c.Register<B>(); c.Register<C>(); c.Register<D>();
        c.Register<E>(); c.Register<F>(); c.Register<G>(); c.Register<H>();
        c.Register<AB>(); c.Register<ABC>(); c.Register<ABCD>();
        c.Register<BCD>(); c.Register<FG>(); c.Register<CDEF>();
    }

    [Test]
    public void should_able_to_register_and_resolve_on_different_thread()
    {
        ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
        Console.WriteLine($"workers = {workerThreads}, ports = {completionPortThreads}");
        var count = _threadCount;

        using var root = new Container();

        for (var i = 0; i < count; i++) new Thread(Run) { Name = $"Thread{i + 1}" }.Start(root);

        while (Interlocked.Read(ref _threadCount) > 0)
        {
            Thread.Sleep(200);
            Console.WriteLine($"remained {Interlocked.Read(ref _threadCount)}");
        }
    }

    void Run(object state)
    {
        try
        {
            var container = (Container)state;
            var rnd = new Random();
            var time = rnd.Next(0, 10000);

            var shouldCreateContainer = rnd.Next(3) == 0;
            if (shouldCreateContainer) container = container.CreateChildContainer();

            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < time)
            {
                var type = s_types[rnd.Next(s_types.Length)];
                switch (rnd.Next(3))
                {
                    case 0:
                        container.Register(type).Transient().AsSelf().AsBases().AsInterfaces();
                        break;
                    case 1:
                        container.Register(type).Singleton().AsSelf().AsBases().AsInterfaces();
                        break;
                    case 2:
                        container.Register(type).Scoped().AsSelf().AsBases().AsInterfaces();
                        break;
                    default:
                        throw new InvalidOperationException("rnd.Next(3) produced a value outside [0, 3).");
                }
                Thread.Sleep(rnd.Next(0, 100));
            }

            time = rnd.Next(0, 10000);
            watch.Restart();
            while (watch.ElapsedMilliseconds < time)
            {
                var type = s_types[rnd.Next(s_types.Length)];
                try
                {
                    Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: resolve {type}");
                    var instances = container.ResolveGroup(type).Append(container.Resolve(type));
                    foreach (var instance in instances)
                        if (!type.IsInstanceOfType(instance))
                            throw new ApplicationException($"{instance} != {type}");
                }
                catch (ArgumentException)
                {
                    // ignore
                }
                Thread.Sleep(rnd.Next(0, 100));
            }

            if (shouldCreateContainer) container.Dispose();
        }
        finally
        {
            var count = Interlocked.Decrement(ref _threadCount);
            Console.WriteLine($"{Thread.CurrentThread.Name} count={count}");
        }
    }

    internal interface IA
    {
    }

    internal interface IB
    {
    }

    internal interface IC
    {
    }

    internal class A : IA
    {
    }

    internal class B : IB
    {
    }

    internal class C : IC
    {
    }

    internal class D : IA, IB, IC
    {
    }

    internal class E : C, IA, IB
    {
    }

    internal class F : E
    {
    }

    internal class G : A, IC
    {
    }

    internal class H : B, IA
    {
    }

    internal class AB : IA
    {
        public AB(A a, B b)
        {
            if (!(a is A)) throw new ApplicationException();
            if (!(b is B)) throw new ApplicationException();
        }
    }

    internal class ABC
    {
        public ABC(A a, B b, C c)
        {
            if (!(a is A)) throw new ApplicationException();
            if (!(b is B)) throw new ApplicationException();
            if (!(c is C)) throw new ApplicationException();
        }
    }

    internal class ABCD
    {
        public ABCD(A a, B b, C c, D d)
        {
            if (!(a is A)) throw new ApplicationException();
            if (!(b is B)) throw new ApplicationException();
            if (!(c is C)) throw new ApplicationException();
            if (!(d is D)) throw new ApplicationException();
        }
    }

    internal class BCD : IA
    {
        public BCD(B b, C c, D d)
        {
            if (!(b is B)) throw new ApplicationException();
            if (!(c is C)) throw new ApplicationException();
            if (!(d is D)) throw new ApplicationException();
        }
    }

    internal class FG : IB, IC
    {
        public FG(F f, G g)
        {
            if (!(f is F)) throw new ApplicationException();
            if (!(g is G)) throw new ApplicationException();
        }
    }

    internal class CDEF : IB, IC
    {
        public CDEF(C c, D d, E e, F f)
        {
            if (!(c is C)) throw new ApplicationException();
            if (!(d is D)) throw new ApplicationException();
            if (!(e is E)) throw new ApplicationException();
            if (!(f is F)) throw new ApplicationException();
        }
    }
}
