using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

public class BenchmarkCreateInstance
{
    class Empty {}

    class Complex
    {
        public int I;
        public long L;
        public double D;
        public string S;
        public Matrix4x4 M;

        public Complex(int i, long l, double d, string s, Matrix4x4 m)
        {
            I = i;
            L = l;
            D = d;
            S = s;
            M = m;
        }
    }
    
    [Test, Performance]
    public void create_empty_instance()
    {
        var type = typeof(Empty);
        var ci = type.GetConstructors()[0];
        var expression = Compile(ci);
        
        Measure.Method(() => new Empty())
            .GC()
            .SampleGroup("new")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => Activator.CreateInstance(type))
            .GC()
            .SampleGroup("Activator.CreateInstance")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => ci.Invoke(Array.Empty<object>()))
            .GC()
            .SampleGroup("Constructor.Invoke")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() =>
            {
                var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                ci.Invoke(instance, Array.Empty<object>());
            })
            .GC()
            .SampleGroup("Constructor.Invoke with instance")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => expression(Array.Empty<object>()))
            .GC()
            .SampleGroup("Expression")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
    }
    
    [Test, Performance]
    public void create_complex_instance()
    {
        var type = typeof(Complex);
        var ci = type.GetConstructors()[0];
        var expression = Compile(ci);
        var args = new object[] { 123, 234L, 111.111, "fjklfd", Matrix4x4.identity };
        
        Measure.Method(() => new Complex(123, 234L, 111.111, "fjklfd", Matrix4x4.identity))
            .GC()
            .SampleGroup("new")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => Activator.CreateInstance(type, args))
            .GC()
            .SampleGroup("Activator.CreateInstance")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => ci.Invoke(args))
            .GC()
            .SampleGroup("Constructor.Invoke")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() =>
            {
                var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                ci.Invoke(instance, args);
            })
            .GC()
            .SampleGroup("Constructor.Invoke with instance")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
        
        Measure.Method(() => expression(args))
            .GC()
            .SampleGroup("Expression")
            .WarmupCount(1)
            .IterationsPerMeasurement(10000)
            .MeasurementCount(5)
            .Run();
    }
    
    Func<object[], object> Compile(ConstructorInfo ci)
    {
        var @params = Expression.Parameter(typeof(object[]));
        var args = ci.GetParameters().Select((parameter, index) => Expression.Convert(
            Expression.ArrayIndex(@params, Expression.Constant(index)),
            parameter.ParameterType)
        ).Cast<Expression>().ToArray();
        var @new = Expression.New(ci, args);
        var lambda = Expression.Lambda(typeof(Func<object[], object>), Expression.Convert(@new, typeof(object)), @params);
        return (Func<object[], object>) lambda.Compile();
    }
}
