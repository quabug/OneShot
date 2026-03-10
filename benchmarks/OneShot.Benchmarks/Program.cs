// See https://aka.ms/new-console-template for more information

using System.Numerics;
using System.Reflection;
using Benchmark;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

Console.WriteLine("Hello, World!");

var summary = BenchmarkRunner.Run(typeof(Program).Assembly);

public class BenchmarkEmpty
{
    class Empty {}
    
    private readonly Type _type;
    private readonly ConstructorInfo _ci;
    private readonly Func<object[], object> _creator;

    public BenchmarkEmpty()
    {
        _type = typeof(Empty);
        _ci = _type.GetConstructors()[0];
        _creator = _ci.Compile();
    }
    
    [Benchmark] public object EmptyNew() => new();
    [Benchmark] public object EmptyCreateInstance() => Activator.CreateInstance(_type);
    [Benchmark] public object EmptyConstructorInvoke() => _ci.Invoke(Array.Empty<object>());
    [Benchmark] public object EmptyConstructorInvokeWithInstance()
    {
        var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(_type);
        return _ci.Invoke(instance, Array.Empty<object>());
    }
    [Benchmark] public object EmptyExpression() => _creator(Array.Empty<object>());
}

public class BenchmarkComplex
{
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
    
    private readonly Type _type;
    private readonly ConstructorInfo _ci;
    private readonly Func<object[], object> _creator;
    private readonly object[] _args;

    public BenchmarkComplex()
    {
        _type = typeof(Complex);
        _ci = _type.GetConstructors()[0];
        _creator = _ci.Compile();
        _args = new object[] { 123, 234L, 111.111, "fjklfd", Matrix4x4.Identity };
    }
    
    [Benchmark] public object ComplexNew() => new Complex(123, 234L, 111.111, "fjklfd", Matrix4x4.Identity);
    [Benchmark] public object ComplexCreateInstance() => Activator.CreateInstance(_type, _args);
    [Benchmark] public object ComplexConstructorInvoke() => _ci.Invoke(_args);
    [Benchmark] public object ComplexConstructorInvokeWithInstance()
    {
        var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(_type);
        return _ci.Invoke(instance, _args);
    }
    [Benchmark] public object ComplexExpression() => _creator(_args);
}
