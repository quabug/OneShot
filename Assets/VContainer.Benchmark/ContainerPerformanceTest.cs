using System;
using NUnit.Framework;
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
            var reflexBuilder = new Reflex.Core.ContainerDescriptor("test");
            reflexBuilder.AddSingleton(typeof(Singleton1), typeof(ISingleton1));
            reflexBuilder.AddSingleton(typeof(Singleton2), typeof(ISingleton2));
            reflexBuilder.AddSingleton(typeof(Singleton3), typeof(ISingleton3));
            var reflexContainer = reflexBuilder.Build();

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

            BenchmarkOneShotMatrix(container =>
            {
                container.Register<Singleton1>().Singleton().As<ISingleton1>();
                container.Register<Singleton2>().Singleton().As<ISingleton2>();
                container.Register<Singleton3>().Singleton().As<ISingleton3>();
                return () =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<ISingleton1>();
                        container.Resolve<ISingleton2>();
                        container.Resolve<ISingleton3>();
                    }
                };
            });
        }

        [Test]
        [Performance]
        public void ResolveTransient()
        {
            var reflexBuilder = new Reflex.Core.ContainerDescriptor("test");
            reflexBuilder.AddTransient(typeof(Transient1), typeof(ITransient1));
            reflexBuilder.AddTransient(typeof(Transient2), typeof(ITransient2));
            reflexBuilder.AddTransient(typeof(Transient3), typeof(ITransient3));
            var reflexContainer = reflexBuilder.Build();

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
            
            BenchmarkOneShotMatrix(container =>
            {
                container.Register<Transient1>().As<ITransient1>();
                container.Register<Transient2>().As<ITransient2>();
                container.Register<Transient3>().As<ITransient3>();
                return () =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<ITransient1>();
                        container.Resolve<ITransient2>();
                        container.Resolve<ITransient3>();
                    }
                };
            });
        }

        [Test]
        [Performance]
        public void ResolveCombined()
        {
            var reflexBuilder = new Reflex.Core.ContainerDescriptor("test");
            reflexBuilder.AddSingleton(typeof(Singleton1), typeof(ISingleton1));
            reflexBuilder.AddSingleton(typeof(Singleton2), typeof(ISingleton2));
            reflexBuilder.AddSingleton(typeof(Singleton3), typeof(ISingleton3));
            reflexBuilder.AddTransient(typeof(Transient1), typeof(ITransient1));
            reflexBuilder.AddTransient(typeof(Transient2), typeof(ITransient2));
            reflexBuilder.AddTransient(typeof(Transient3), typeof(ITransient3));
            reflexBuilder.AddTransient(typeof(Combined1), typeof(ICombined1));
            reflexBuilder.AddTransient(typeof(Combined2), typeof(ICombined2));
            reflexBuilder.AddTransient(typeof(Combined3), typeof(ICombined3));
            var reflexContainer = reflexBuilder.Build();

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
            
            BenchmarkOneShotMatrix(container =>
            {
                container.Register<Singleton1>().Singleton().As<ISingleton1>();
                container.Register<Singleton2>().Singleton().As<ISingleton2>();
                container.Register<Singleton3>().Singleton().As<ISingleton3>();
                container.Register<Transient1>().Transient().As<ITransient1>();
                container.Register<Transient2>().Transient().As<ITransient2>();
                container.Register<Transient3>().Transient().As<ITransient3>();
                container.Register<Combined1>().Transient().As<ICombined1>();
                container.Register<Combined2>().Transient().As<ICombined2>();
                container.Register<Combined3>().Transient().As<ICombined3>();
                return () =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<ICombined1>();
                        container.Resolve<ICombined2>();
                        container.Resolve<ICombined3>();
                    }
                };
            });

        }

        [Test]
        [Performance]
        public void ResolveComplex()
        {
            var reflexBuilder = new Reflex.Core.ContainerDescriptor("test");
            reflexBuilder.AddSingleton(typeof(FirstService), typeof(IFirstService));
            reflexBuilder.AddSingleton(typeof(SecondService), typeof(ISecondService));
            reflexBuilder.AddSingleton(typeof(ThirdService), typeof(IThirdService));
            reflexBuilder.AddTransient(typeof(SubObjectA), typeof(ISubObjectA));
            reflexBuilder.AddTransient(typeof(SubObjectB), typeof(ISubObjectB));
            reflexBuilder.AddTransient(typeof(SubObjectC), typeof(ISubObjectC));
            reflexBuilder.AddTransient(typeof(Complex1), typeof(IComplex1));
            reflexBuilder.AddTransient(typeof(Complex2), typeof(IComplex2));
            reflexBuilder.AddTransient(typeof(Complex3), typeof(IComplex3));
            reflexBuilder.AddTransient(typeof(SubObjectOne), typeof(ISubObjectOne));
            reflexBuilder.AddTransient(typeof(SubObjectTwo), typeof(ISubObjectTwo));
            reflexBuilder.AddTransient(typeof(SubObjectThree), typeof(ISubObjectThree));
            var reflexContainer = reflexBuilder.Build();

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
            
            BenchmarkOneShotMatrix(container =>
            {
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
                return () =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        container.Resolve<IComplex1>();
                        container.Resolve<IComplex2>();
                        container.Resolve<IComplex3>();
                    }
                };
            });
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
                        var reflexBuilder = new Reflex.Core.ContainerDescriptor("test");
                        reflexBuilder.AddSingleton(typeof(FirstService), typeof(IFirstService));
                        reflexBuilder.AddSingleton(typeof(SecondService), typeof(ISecondService));
                        reflexBuilder.AddSingleton(typeof(ThirdService), typeof(IThirdService));
                        reflexBuilder.AddTransient(typeof(SubObjectA), typeof(ISubObjectA));
                        reflexBuilder.AddTransient(typeof(SubObjectB), typeof(ISubObjectB));
                        reflexBuilder.AddTransient(typeof(SubObjectC), typeof(ISubObjectC));
                        reflexBuilder.AddTransient(typeof(Complex1), typeof(IComplex1));
                        reflexBuilder.AddTransient(typeof(Complex2), typeof(IComplex2));
                        reflexBuilder.AddTransient(typeof(Complex3), typeof(IComplex3));
                        reflexBuilder.AddTransient(typeof(SubObjectOne), typeof(ISubObjectOne));
                        reflexBuilder.AddTransient(typeof(SubObjectTwo), typeof(ISubObjectTwo));
                        reflexBuilder.AddTransient(typeof(SubObjectThree), typeof(ISubObjectThree));
                        _ = reflexBuilder.Build();
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
            
            Measure
                .Method(() =>
                {
                    for (var i = 0; i < N; i++)
                    {
                        var container = new OneShot.Container { PreAllocateArgumentArrayOnRegister = true };
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
                .SampleGroup("OneShot pre-allocate arguments array")
                .GC()
                .Run();
        }

        void BenchmarkOneShotMatrix(Func<OneShot.Container, Action> register)
        {
            var matrix = new[]
            {
                (new OneShot.Container(), "OneShot"),
                (new OneShot.Container { EnableCircularCheck = false }, "OneShot:circular-check=false"),
                (new OneShot.Container { PreAllocateArgumentArrayOnRegister = true }, "OneShot:preallocate=true"),
                (new OneShot.Container { EnableCircularCheck = false, PreAllocateArgumentArrayOnRegister = true }, "OneShot:preallocate=true circular-check=false"),
            };
            foreach (var (container, name) in matrix)
            {
                var action = register(container);
                Measure
                    .Method(action)
                    .SampleGroup(name)
                    .GC()
                    .Run();
            }
        }
    }
}
