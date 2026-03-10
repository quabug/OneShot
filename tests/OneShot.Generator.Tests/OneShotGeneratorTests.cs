using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace OneShot.Generator.Tests;

[TestFixture]
public class OneShotGeneratorTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Get references from the runtime
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Container).Assembly.Location),
        };

        // Add runtime assemblies needed for compilation
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.Concurrent.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.Expressions.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.ComponentModel.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Threading.dll")));

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new OneShotGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        // Verify no generator errors
        var generatorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.That(generatorDiagnostics, Is.Empty, "Generator produced errors");

        return driver.GetRunResult();
    }

    [Test]
    public void should_generate_for_inject_constructor()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    public Dep Dep { get; }
    [Inject] public Service(Dep dep) => Dep = dep;
    public Service() { }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("container.Resolve<global::Dep>()"));
        Assert.That(text, Does.Contain("new global::Service("));
        Assert.That(text, Does.Contain("TypeInfoRegistry.Register"));
        Assert.That(text, Does.Contain("ModuleInitializer"));
    }

    [Test]
    public void should_generate_for_injectable_attribute()
    {
        var source = @"
using OneShot;

[Injectable]
public class SimpleService { }
";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("new global::SimpleService()"));
    }

    [Test]
    public void should_generate_for_inject_fields_and_properties()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public Dep FieldDep;
    [Inject] public Dep PropDep { get; set; }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("target.FieldDep = container.Resolve<global::Dep>()"));
        Assert.That(text, Does.Contain("target.PropDep = container.Resolve<global::Dep>()"));
    }

    [Test]
    public void should_generate_for_inject_method()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public void Init(Dep dep) { }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("target.Init("));
        Assert.That(text, Does.Contain("container.Resolve<global::Dep>()"));
    }

    [Test]
    public void should_handle_labeled_parameters()
    {
        var source = @"
using OneShot;

public class Dep { }
public interface MyLabel : ILabel<Dep> { }
public class Service
{
    [Inject] public Service([Inject(typeof(MyLabel))] Dep dep) { }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("typeof(global::MyLabel)"));
    }

    [Test]
    public void should_handle_default_parameters()
    {
        var source = @"
using OneShot;

public class Dep { }

[Injectable]
public class Service
{
    public Dep Dep { get; }
    public int Value { get; }
    public Service(Dep dep, int value = 42) { Dep = dep; Value = value; }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("container.Resolve<global::Dep>()"));
        Assert.That(text, Does.Contain("TryResolve"));
        Assert.That(text, Does.Contain("42"));
    }

    [Test]
    public void should_skip_private_nested_types()
    {
        var source = @"
using OneShot;

public class Outer
{
    private class Inner
    {
        [Inject] public void Init() { }
    }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(0));
    }

    [Test]
    public void should_skip_abstract_types()
    {
        var source = @"
using OneShot;

[Injectable]
public abstract class AbstractService { }
";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(0));
    }

    [Test]
    public void should_skip_generic_type_definitions()
    {
        var source = @"
using OneShot;

[Injectable]
public class GenericService<T> { }
";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(0));
    }

    [Test]
    public void should_generate_unsafe_accessor_for_private_setter()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public Dep Prop { get; private set; }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("UnsafeAccessor"));
        Assert.That(text, Does.Contain("UnsafeSet_Prop"));
    }

    [Test]
    public void should_include_interfaces_and_base_types()
    {
        var source = @"
using OneShot;

public interface IService { }
public class BaseService { }
public class Service : BaseService, IService
{
    [Inject] public Service() { }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(1));
        var text = result.GeneratedTrees[0].GetText().ToString();
        Assert.That(text, Does.Contain("typeof(global::IService)"));
        Assert.That(text, Does.Contain("typeof(global::BaseService)"));
    }

    [Test]
    public void should_not_generate_for_types_without_inject()
    {
        var source = @"
public class PlainService
{
    public PlainService(int value) { }
}";
        var result = RunGenerator(source);
        Assert.That(result.GeneratedTrees.Length, Is.EqualTo(0));
    }
}
