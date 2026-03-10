using System;

namespace OneShot.Test
{
    internal interface InterfaceA
    {
    }

    internal class TypeA : InterfaceA
    {
    }

    internal class DefaultConstructor
    {
        public readonly TypeA TypeA;
        public DefaultConstructor(TypeA typeA) => TypeA = typeA;
        public int GetIntValue() => 100;
    }

    internal class InjectConstructor
    {
        public readonly TypeA TypeA;

        [Inject]
        public InjectConstructor(TypeA typeA) => TypeA = typeA;

        public InjectConstructor(DefaultConstructor defaultConstructor) => TypeA = null;
    }

    internal class ConstructorWithDefaultParameter
    {
        public readonly TypeA TypeA;
        public readonly int IntValue = 0;

        public ConstructorWithDefaultParameter(TypeA typeA, int intValue = 10)
        {
            TypeA = typeA;
            IntValue = intValue;
        }
    }

    internal class ComplexClass
    {
        public readonly TypeA A;
        public readonly InterfaceA B;
        public readonly InjectConstructor C;
        public readonly float D;
        public readonly ConstructorWithDefaultParameter E;
        public readonly Func<int> GetIntValue;

        public ComplexClass(TypeA a, InterfaceA b, InjectConstructor c, float d = 22,
            ConstructorWithDefaultParameter e = null, Func<int> getIntValue = null)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            GetIntValue = getIntValue;
        }
    }
    
    internal class Disposable : IDisposable
    {
        public int DisposedCount = 0;
        public void Dispose() => DisposedCount++;
    }

    internal class InjectInt
    {
        public int Value;
        public InjectInt(int value) => Value = value;
    }
}