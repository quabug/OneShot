using System.Linq.Expressions;
using System.Reflection;

namespace Benchmark;

public static class Extension
{
    public static Func<object[], object> Compile(this ConstructorInfo ci)
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