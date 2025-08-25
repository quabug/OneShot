#if !UNITY_5_3_OR_NEWER
using NUnit.Framework;

namespace OneShot.Test
{
    public static class Assert
    {
        public static void AreSame(object expected, object actual)
        {
            NUnit.Framework.Assert.That(actual, Is.SameAs(expected));
        }

        public static void AreSame(object expected, object actual, string message)
        {
            NUnit.Framework.Assert.That(actual, Is.SameAs(expected), message);
        }

        public static void AreNotSame(object expected, object actual)
        {
            NUnit.Framework.Assert.That(actual, Is.Not.SameAs(expected));
        }

        public static void AreNotSame(object expected, object actual, string message)
        {
            NUnit.Framework.Assert.That(actual, Is.Not.SameAs(expected), message);
        }

        public static void AreEqual<T>(T expected, T actual)
        {
            NUnit.Framework.Assert.That(actual, Is.EqualTo(expected));
        }

        public static void AreEqual<T>(T expected, T actual, string message)
        {
            NUnit.Framework.Assert.That(actual, Is.EqualTo(expected), message);
        }

        public static void IsTrue(bool condition)
        {
            NUnit.Framework.Assert.That(condition, Is.True);
        }

        public static void IsTrue(bool condition, string message)
        {
            NUnit.Framework.Assert.That(condition, Is.True, message);
        }

        public static void IsFalse(bool condition)
        {
            NUnit.Framework.Assert.That(condition, Is.False);
        }

        public static void IsFalse(bool condition, string message)
        {
            NUnit.Framework.Assert.That(condition, Is.False, message);
        }

        public static void IsNull(object value)
        {
            NUnit.Framework.Assert.That(value, Is.Null);
        }

        public static void IsNull(object value, string message)
        {
            NUnit.Framework.Assert.That(value, Is.Null, message);
        }

        public static void IsNotNull(object value)
        {
            NUnit.Framework.Assert.That(value, Is.Not.Null);
        }

        public static void IsNotNull(object value, string message)
        {
            NUnit.Framework.Assert.That(value, Is.Not.Null, message);
        }

        public static void Throws<T>(NUnit.Framework.TestDelegate code) where T : System.Exception
        {
            NUnit.Framework.Assert.Throws<T>(code);
        }

        public static T Catch<T>(NUnit.Framework.TestDelegate code) where T : System.Exception
        {
            return NUnit.Framework.Assert.Catch<T>(code);
        }

        public static void DoesNotThrow(NUnit.Framework.TestDelegate code)
        {
            NUnit.Framework.Assert.DoesNotThrow(code);
        }

        public static void That<T>(T actual, NUnit.Framework.Constraints.IResolveConstraint expression)
        {
            NUnit.Framework.Assert.That(actual, expression);
        }

        public static void That<T>(T actual, NUnit.Framework.Constraints.IResolveConstraint expression, string message)
        {
            NUnit.Framework.Assert.That(actual, expression, message);
        }

        public static void That(NUnit.Framework.TestDelegate code, NUnit.Framework.Constraints.IResolveConstraint expression)
        {
            NUnit.Framework.Assert.That(code, expression);
        }
    }
}
#endif