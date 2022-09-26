using NUnit.Framework;
using OneShot;
using Unity.PerformanceTesting;
using VContainer;
using VContainer.Benchmark.Fixtures;

namespace VvContainer.Benchmark
{
    public class ContainerPerformanceTest
    {
        const int N = 10_000;

        [Test]
        [Performance]
        public void ResolveSingleton()
        {
            var zenjectContainer = new Zenject.DiContainer();
            zenjectContainer.Bind<ISingleton1>().To<Singleton1>().AsSingle();
            zenjectContainer.Bind<ISingleton2>().To<Singleton2>().AsSingle();
            zenjectContainer.Bind<ISingleton3>().To<Singleton3>().AsSingle();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        zenjectContainer.Resolve<ISingleton1>();
                        zenjectContainer.Resolve<ISingleton2>();
                        zenjectContainer.Resolve<ISingleton3>();
                    }
                })
                .SampleGroup("Zenject")
                .GC()
                .Run();

            var reflexContainer = new Reflex.Container("test");
            reflexContainer.BindSingleton<ISingleton1, Singleton1>();
            reflexContainer.BindSingleton<ISingleton2, Singleton2>();
            reflexContainer.BindSingleton<ISingleton3, Singleton3>();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        reflexContainer.Resolve<ISingleton1>();
                        reflexContainer.Resolve<ISingleton2>();
                        reflexContainer.Resolve<ISingleton3>();
                    }
                })
                .SampleGroup("Reflex")
                .GC()
                .Run();

            var builder = new ContainerBuilder();
            builder.Register<ISingleton1, Singleton1>(Lifetime.Singleton);
            builder.Register<ISingleton2, Singleton2>(Lifetime.Singleton);
            builder.Register<ISingleton3, Singleton3>(Lifetime.Singleton);
            var vContainer = builder.Build();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        vContainer.Resolve<ISingleton1>();
                        vContainer.Resolve<ISingleton2>();
                        vContainer.Resolve<ISingleton3>();
                    }
                })
                .SampleGroup("VContainer")
                .GC()
                .Run();
            
            var container = new OneShot.Container();
            container.Register<Singleton1>().Singleton().As<ISingleton1>();
            container.Register<Singleton2>().Singleton().As<ISingleton2>();
            container.Register<Singleton3>().Singleton().As<ISingleton3>();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<ISingleton1>();
                        container.Resolve<ISingleton2>();
                        container.Resolve<ISingleton3>();
                    }
                })
                .SampleGroup("OneShot")
                .GC()
                .Run();
        }

        [Test]
        [Performance]
        public void ResolveTransient()
        {
            var zenjectContainer = new Zenject.DiContainer();
            zenjectContainer.Bind<ITransient1>().To<Transient1>().AsTransient();
            zenjectContainer.Bind<ITransient2>().To<Transient2>().AsTransient();
            zenjectContainer.Bind<ITransient3>().To<Transient3>().AsTransient();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        zenjectContainer.Resolve<ITransient1>();
                        zenjectContainer.Resolve<ITransient2>();
                        zenjectContainer.Resolve<ITransient3>();
                    }
                })
                .SampleGroup("Zenject")
                .GC()
                .Run();

            var reflexContainer = new Reflex.Container("test");
            reflexContainer.BindTransient<ITransient1, Transient1>();
            reflexContainer.BindTransient<ITransient2, Transient2>();
            reflexContainer.BindTransient<ITransient3, Transient3>();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        reflexContainer.Resolve<ITransient1>();
                        reflexContainer.Resolve<ITransient2>();
                        reflexContainer.Resolve<ITransient3>();
                    }
                })
                .SampleGroup("Reflex")
                .GC()
                .Run();

            var builder = new ContainerBuilder();
            builder.Register<ITransient1, Transient1>(Lifetime.Transient);
            builder.Register<ITransient2, Transient2>(Lifetime.Transient);
            builder.Register<ITransient3, Transient3>(Lifetime.Transient);
            var vContainer = builder.Build();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        vContainer.Resolve<ITransient1>();
                        vContainer.Resolve<ITransient2>();
                        vContainer.Resolve<ITransient3>();
                    }
                })
                .SampleGroup("VContainer")
                .GC()
                .Run();
            
            var container = new OneShot.Container();
            container.Register<Transient1>().As<ITransient1>();
            container.Register<Transient2>().As<ITransient2>();
            container.Register<Transient3>().As<ITransient3>();
            
            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<ITransient1>();
                        container.Resolve<ITransient2>();
                        container.Resolve<ITransient3>();
                    }
                })
                .SampleGroup("OneShot")
                .GC()
                .Run();
        }

        [Test]
        [Performance]
        public void ResolveCombined()
        {
            var zenjectContainer = new Zenject.DiContainer();
            zenjectContainer.Bind<ISingleton1>().To<Singleton1>().AsSingle();
            zenjectContainer.Bind<ISingleton2>().To<Singleton2>().AsSingle();
            zenjectContainer.Bind<ISingleton3>().To<Singleton3>().AsSingle();
            zenjectContainer.Bind<ITransient1>().To<Transient1>().AsTransient();
            zenjectContainer.Bind<ITransient2>().To<Transient2>().AsTransient();
            zenjectContainer.Bind<ITransient3>().To<Transient3>().AsTransient();
            zenjectContainer.Bind<ICombined1>().To<Combined1>().AsTransient();
            zenjectContainer.Bind<ICombined2>().To<Combined2>().AsTransient();
            zenjectContainer.Bind<ICombined3>().To<Combined3>().AsTransient();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        zenjectContainer.Resolve<ICombined1>();
                        zenjectContainer.Resolve<ICombined2>();
                        zenjectContainer.Resolve<ICombined3>();
                    }
                })
                .SampleGroup("Zenject")
                .GC()
                .Run();

            var reflexContainer = new Reflex.Container("test");
            reflexContainer.BindSingleton<ISingleton1, Singleton1>();
            reflexContainer.BindSingleton<ISingleton2, Singleton2>();
            reflexContainer.BindSingleton<ISingleton3, Singleton3>();
            reflexContainer.BindTransient<ITransient1, Transient1>();
            reflexContainer.BindTransient<ITransient2, Transient2>();
            reflexContainer.BindTransient<ITransient3, Transient3>();
            reflexContainer.BindTransient<ICombined1, Combined1>();
            reflexContainer.BindTransient<ICombined2, Combined2>();
            reflexContainer.BindTransient<ICombined3, Combined3>();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        reflexContainer.Resolve<ICombined1>();
                        reflexContainer.Resolve<ICombined2>();
                        reflexContainer.Resolve<ICombined3>();
                    }
                })
                .SampleGroup("Reflex")
                .GC()
                .Run();

            var builder = new ContainerBuilder();
            builder.Register<ISingleton1, Singleton1>(Lifetime.Singleton);
            builder.Register<ISingleton2, Singleton2>(Lifetime.Singleton);
            builder.Register<ISingleton3, Singleton3>(Lifetime.Singleton);
            builder.Register<ITransient1, Transient1>(Lifetime.Transient);
            builder.Register<ITransient2, Transient2>(Lifetime.Transient);
            builder.Register<ITransient3, Transient3>(Lifetime.Transient);
            builder.Register<ICombined1, Combined1>(Lifetime.Transient);
            builder.Register<ICombined2, Combined2>(Lifetime.Transient);
            builder.Register<ICombined3, Combined3>(Lifetime.Transient);
            var vContainer = builder.Build();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        vContainer.Resolve<ICombined1>();
                        vContainer.Resolve<ICombined2>();
                        vContainer.Resolve<ICombined3>();
                    }
                })
                .SampleGroup("VContainer")
                .GC()
                .Run();
        }

        [Test]
        [Performance]
        public void ResolveComplex()
        {
            var zenjectContainer = new Zenject.DiContainer();
            zenjectContainer.Bind<IFirstService>().To<FirstService>().AsSingle();
            zenjectContainer.Bind<ISecondService>().To<SecondService>().AsSingle();
            zenjectContainer.Bind<IThirdService>().To<ThirdService>().AsSingle();
            zenjectContainer.Bind<ISubObjectA>().To<SubObjectA>().AsTransient();
            zenjectContainer.Bind<ISubObjectB>().To<SubObjectB>().AsTransient();
            zenjectContainer.Bind<ISubObjectC>().To<SubObjectC>().AsTransient();
            zenjectContainer.Bind<IComplex1>().To<Complex1>().AsTransient();
            zenjectContainer.Bind<IComplex2>().To<Complex2>().AsTransient();
            zenjectContainer.Bind<IComplex3>().To<Complex3>().AsTransient();
            zenjectContainer.Bind<ISubObjectOne>().To<SubObjectOne>().AsTransient();
            zenjectContainer.Bind<ISubObjectTwo>().To<SubObjectTwo>().AsTransient();
            zenjectContainer.Bind<ISubObjectThree>().To<SubObjectThree>().AsTransient();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        zenjectContainer.Resolve<IComplex1>();
                        zenjectContainer.Resolve<IComplex2>();
                        zenjectContainer.Resolve<IComplex3>();
                    }
                })
                .SampleGroup("Zenject")
                .GC()
                .Run();

            var reflexContainer = new Reflex.Container("test");
            reflexContainer.BindSingleton<IFirstService, FirstService>();
            reflexContainer.BindSingleton<ISecondService, SecondService>();
            reflexContainer.BindSingleton<IThirdService, ThirdService>();
            reflexContainer.BindTransient<ISubObjectA, SubObjectA>();
            reflexContainer.BindTransient<ISubObjectB, SubObjectB>();
            reflexContainer.BindTransient<ISubObjectC, SubObjectC>();
            reflexContainer.BindTransient<IComplex1, Complex1>();
            reflexContainer.BindTransient<IComplex2, Complex2>();
            reflexContainer.BindTransient<IComplex3, Complex3>();
            reflexContainer.BindTransient<ISubObjectOne, SubObjectOne>();
            reflexContainer.BindTransient<ISubObjectTwo, SubObjectTwo>();
            reflexContainer.BindTransient<ISubObjectThree, SubObjectThree>();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        reflexContainer.Resolve<IComplex1>();
                        reflexContainer.Resolve<IComplex2>();
                        reflexContainer.Resolve<IComplex3>();
                    }
                })
                .SampleGroup("Reflex")
                .GC()
                .Run();

            var builder = new ContainerBuilder();
            builder.Register<IFirstService, FirstService>(Lifetime.Singleton);
            builder.Register<ISecondService, SecondService>(Lifetime.Singleton);
            builder.Register<IThirdService, ThirdService>(Lifetime.Singleton);
            builder.Register<ISubObjectA, SubObjectA>(Lifetime.Transient);
            builder.Register<ISubObjectB, SubObjectB>(Lifetime.Transient);
            builder.Register<ISubObjectC, SubObjectC>(Lifetime.Transient);
            builder.Register<IComplex1, Complex1>(Lifetime.Transient);
            builder.Register<IComplex2, Complex2>(Lifetime.Transient);
            builder.Register<IComplex3, Complex3>(Lifetime.Transient);
            builder.Register<ISubObjectOne, SubObjectOne>(Lifetime.Transient);
            builder.Register<ISubObjectTwo, SubObjectTwo>(Lifetime.Transient);
            builder.Register<ISubObjectThree, SubObjectThree>(Lifetime.Transient);
            var vContainer = builder.Build();

            Measure
                .Method(() =>
                {
                    // UnityEngine.Profiling.Profiler.BeginSample("VContainer Resolve(Complex)");
                    for (var i = 0; i < N; i++)
                    {
                        vContainer.Resolve<IComplex1>();
                        vContainer.Resolve<IComplex2>();
                        vContainer.Resolve<IComplex3>();
                    }
                    // UnityEngine.Profiling.Profiler.EndSample();
                })
                .SampleGroup("VContainer")
                .GC()
                .Run();
            
            var container = new OneShot.Container();
            container.Register<FirstService>().Singleton().As<IFirstService>();
            container.Register<SecondService>().Singleton().As<ISecondService>();
            container.Register<ThirdService>().Singleton().As<IThirdService>();
            container.Register<SubObjectA>().Transient().As<ISubObjectA>();
            container.Register<SubObjectB>().Transient().As<ISubObjectB>();
            container.Register<SubObjectC>().Transient().As<ISubObjectC>();
            container.Register<Complex1>().Transient().As<IComplex1>();
            container.Register<Complex2>().Transient().As<IComplex2>();
            container.Register<Complex3>().Transient().As<IComplex3>();
            container.Register<SubObjectOne>().Transient().As<ISubObjectOne>();
            container.Register<SubObjectTwo>().Transient().As<ISubObjectTwo>();
            container.Register<SubObjectThree>().Transient().As<ISubObjectThree>();

            Measure
                .Method(() =>
                {
                    // UnityEngine.Profiling.Profiler.BeginSample("VContainer Resolve(Complex)");
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<IComplex1>();
                        container.Resolve<IComplex2>();
                        container.Resolve<IComplex3>();
                    }
                    // UnityEngine.Profiling.Profiler.EndSample();
                })
                .SampleGroup("OneShot")
                .GC()
                .Run();
        }

        // [Test]
        // [Performance]
        // public void ResolveComplex_VContainer()
        // {
        //     var builder = new ContainerBuilder();
        //     builder.Register<IFirstService, FirstService>(Lifetime.Singleton);
        //     builder.Register<ISecondService, SecondService>(Lifetime.Singleton);
        //     builder.Register<IThirdService, ThirdService>(Lifetime.Singleton);
        //     builder.Register<ISubObjectA, SubObjectA>(Lifetime.Transient);
        //     builder.Register<ISubObjectB, SubObjectB>(Lifetime.Transient);
        //     builder.Register<ISubObjectC, SubObjectC>(Lifetime.Transient);
        //     builder.Register<IComplex1, Complex1>(Lifetime.Transient);
        //     builder.Register<IComplex2, Complex2>(Lifetime.Transient);
        //     builder.Register<IComplex3, Complex3>(Lifetime.Transient);
        //     builder.Register<ISubObjectOne, SubObjectOne>(Lifetime.Transient);
        //     builder.Register<ISubObjectTwo, SubObjectTwo>(Lifetime.Transient);
        //     builder.Register<ISubObjectThree, SubObjectThree>(Lifetime.Transient);
        //     var vContainer = builder.Build();
        //
        //     Measure
        //         .Method(() =>
        //         {
        //             UnityEngine.Profiling.Profiler.BeginSample("VContainer");
        //             vContainer.Resolve<IComplex1>();
        //             vContainer.Resolve<IComplex2>();
        //             vContainer.Resolve<IComplex3>();
        //             UnityEngine.Profiling.Profiler.EndSample();
        //         })
        //         .Run();
        // }

        [Test]
        [Performance]
        public void ContainerBuildComplex()
        {
            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        var zenjectContainer = new Zenject.DiContainer();
                        zenjectContainer.Bind<IFirstService>().To<FirstService>().AsSingle();
                        zenjectContainer.Bind<ISecondService>().To<SecondService>().AsSingle();
                        zenjectContainer.Bind<IThirdService>().To<ThirdService>().AsSingle();
                        zenjectContainer.Bind<ISubObjectA>().To<SubObjectA>().AsTransient();
                        zenjectContainer.Bind<ISubObjectB>().To<SubObjectB>().AsTransient();
                        zenjectContainer.Bind<ISubObjectC>().To<SubObjectC>().AsTransient();
                        zenjectContainer.Bind<IComplex1>().To<Complex1>().AsTransient();
                        zenjectContainer.Bind<IComplex2>().To<Complex2>().AsTransient();
                        zenjectContainer.Bind<IComplex3>().To<Complex3>().AsTransient();
                        zenjectContainer.Bind<ISubObjectOne>().To<SubObjectOne>().AsTransient();
                        zenjectContainer.Bind<ISubObjectTwo>().To<SubObjectTwo>().AsTransient();
                        zenjectContainer.Bind<ISubObjectThree>().To<SubObjectThree>().AsTransient();
                    }
                })
                .SampleGroup("Zenject")
                .GC()
                .Run();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        var reflexContainer = new Reflex.Container("test");
                        reflexContainer.BindSingleton<IFirstService, FirstService>();
                        reflexContainer.BindSingleton<ISecondService, SecondService>();
                        reflexContainer.BindSingleton<IThirdService, ThirdService>();
                        reflexContainer.BindTransient<ISubObjectA, SubObjectA>();
                        reflexContainer.BindTransient<ISubObjectB, SubObjectB>();
                        reflexContainer.BindTransient<ISubObjectC, SubObjectC>();
                        reflexContainer.BindTransient<IComplex1, Complex1>();
                        reflexContainer.BindTransient<IComplex2, Complex2>();
                        reflexContainer.BindTransient<IComplex3, Complex3>();
                        reflexContainer.BindTransient<ISubObjectOne, SubObjectOne>();
                        reflexContainer.BindTransient<ISubObjectTwo, SubObjectTwo>();
                        reflexContainer.BindTransient<ISubObjectThree, SubObjectThree>();
                    }
                })
                .SampleGroup("Reflex")
                .GC()
                .Run();

            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        var builder = new ContainerBuilder();
                        builder.Register<IFirstService, FirstService>(Lifetime.Singleton);
                        builder.Register<ISecondService, SecondService>(Lifetime.Singleton);
                        builder.Register<IThirdService, ThirdService>(Lifetime.Singleton);
                        builder.Register<ISubObjectA, SubObjectA>(Lifetime.Transient);
                        builder.Register<ISubObjectB, SubObjectB>(Lifetime.Transient);
                        builder.Register<ISubObjectC, SubObjectC>(Lifetime.Transient);
                        builder.Register<IComplex1, Complex1>(Lifetime.Transient);
                        builder.Register<IComplex2, Complex2>(Lifetime.Transient);
                        builder.Register<IComplex3, Complex3>(Lifetime.Transient);
                        builder.Register<ISubObjectOne, SubObjectOne>(Lifetime.Transient);
                        builder.Register<ISubObjectTwo, SubObjectTwo>(Lifetime.Transient);
                        builder.Register<ISubObjectThree, SubObjectThree>(Lifetime.Transient);
                        _ = builder.Build();
                    }
                })
                .SampleGroup("VContainer")
                .GC()
                .Run();
            
            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        var container = new OneShot.Container();
                        container.Register<FirstService>().Singleton().As<IFirstService>();
                        container.Register<SecondService>().Singleton().As<ISecondService>();
                        container.Register<ThirdService>().Singleton().As<IThirdService>();
                        container.Register<SubObjectA>().Transient().As<ISubObjectA>();
                        container.Register<SubObjectB>().Transient().As<ISubObjectB>();
                        container.Register<SubObjectC>().Transient().As<ISubObjectC>();
                        container.Register<Complex1>().Transient().As<IComplex1>();
                        container.Register<Complex2>().Transient().As<IComplex2>();
                        container.Register<Complex3>().Transient().As<IComplex3>();
                        container.Register<SubObjectOne>().Transient().As<ISubObjectOne>();
                        container.Register<SubObjectTwo>().Transient().As<ISubObjectTwo>();
                        container.Register<SubObjectThree>().Transient().As<ISubObjectThree>();
                    }
                })
                .SampleGroup("OneShot")
                .GC()
                .Run();
        }
    }
}
