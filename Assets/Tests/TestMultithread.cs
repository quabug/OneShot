using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace OneShot.Test
{
    public class TestMultiThread
    {
        long _threadCount = 100;
        private Type[] _types;

        [SetUp]
        public void SetUp()
        {
            _types = GetType().GetNestedTypes(BindingFlags.NonPublic).Where(type => !type.IsInterface).ToArray();
        }
        //
        // [Test]
        // public void should_able_to_register_()
        // {
        //     var container = new Container();
        //     container.Register<A>().Singleton().AsSelf().AsBases().AsInterfaces();
        //     container.Register<B>().Singleton().AsSelf().AsBases().AsInterfaces();
        //     container.Register<C>().Singleton().AsSelf().AsBases().AsInterfaces();
        //     container.Register<D>().Singleton().AsSelf().AsBases().AsInterfaces();
        //     container.Register<F>().AsSelf().AsBases().AsInterfaces();
        //     container.Register<E>().AsSelf().AsBases().AsInterfaces();
        //     container.Register<CDEF>().AsSelf().AsBases().AsInterfaces();
        //     container.Resolve<CDEF>();
        // }
        
        [Test]
        public void should_able_to_register_and_resolve_on_different_thread()
        {
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            Console.WriteLine($"workers = {workerThreads}, ports = {completionPortThreads}");
            var count = _threadCount;

            var root = new Container();

            for(var i = 0; i < count; i++) new Thread(Run) { Name = $"Thread{i + 1}" }.Start(root);

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
                    var type = _types[rnd.Next(_types.Length)];
                    switch (rnd.Next(3))
                    {
                        case 0: 
                            container.Register(type).Transient().AsSelf().AsBases().AsInterfaces();
                            break;
                        case 1:
                            container.Register(type).Singleton().AsSelf().AsBases().AsInterfaces();
                            break;
                        case 2:
                            container.Register(type).Scope().AsSelf().AsBases().AsInterfaces();
                            break;
                    }
                    Thread.Sleep(rnd.Next(0, 100));
                }

                time = rnd.Next(0, 10000);
                watch.Restart();
                while (watch.ElapsedMilliseconds < time)
                {
                    var type = _types[rnd.Next(_types.Length)];
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
        
        interface IA {}
        interface IB {}
        interface IC {}
        class A : IA {}
        class B : IB {}
        class C : IC {}
        class D : IA, IB, IC {}
        class E : C, IA, IB {}
        class F : E {}
        class G : A, IC {}
        class H : B, IA {}

        class AB : IA
        {
            public AB(A a, B b)
            {
                if (!(a is A)) throw new ApplicationException();
                if (!(b is B)) throw new ApplicationException();
            }
        }
        
        class ABC 
        {
            public ABC(A a, B b, C c)
            {
                if (!(a is A)) throw new ApplicationException();
                if (!(b is B)) throw new ApplicationException();
                if (!(c is C)) throw new ApplicationException();
            }
        }
        
        class ABCD 
        {
            public ABCD(A a, B b, C c, D d)
            {
                if (!(a is A)) throw new ApplicationException();
                if (!(b is B)) throw new ApplicationException();
                if (!(c is C)) throw new ApplicationException();
                if (!(d is D)) throw new ApplicationException();
            }
        }
        
        class BCD : IA
        {
            public BCD(B b, C c, D d)
            {
                if (!(b is B)) throw new ApplicationException();
                if (!(c is C)) throw new ApplicationException();
                if (!(d is D)) throw new ApplicationException();
            }
        }
        
        class FG : IB, IC
        {
            public FG(F f, G g)
            {
                if (!(f is F)) throw new ApplicationException();
                if (!(g is G)) throw new ApplicationException();
            }
        }
        
        class CDEF : IB, IC
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
}
