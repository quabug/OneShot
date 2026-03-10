using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OneShot.Generator.Tests;

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

    private static async Task<GeneratorDriverRunResult> RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new OneShotGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        // Verify no generator errors
        var generatorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(generatorDiagnostics).IsEmpty();

        return driver.GetRunResult();
    }

    [Test]
    public async Task should_generate_for_inject_constructor()
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
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("container.Resolve<global::Dep>()");
        await Assert.That(text).Contains("new global::Service(");
        await Assert.That(text).Contains("TypeInfoRegistry.Register");
        await Assert.That(text).Contains("ModuleInitializer");
    }

    [Test]
    public async Task should_generate_for_register_call_site()
    {
        var source = @"
using OneShot;

public class SimpleService { }

public class Setup
{
    void Configure(Container c)
    {
        c.Register<SimpleService>();
    }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("new global::SimpleService()");
    }

    [Test]
    public async Task should_generate_for_instantiate_call_site()
    {
        var source = @"
using OneShot;

public class SimpleService { }

public class Setup
{
    void Configure(Container c)
    {
        c.Instantiate<SimpleService>();
    }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("new global::SimpleService()");
    }

    [Test]
    public async Task should_not_generate_for_register_with_factory()
    {
        var source = @"
using OneShot;

public class SimpleService { }

public class Setup
{
    void Configure(Container c)
    {
        c.Register<SimpleService>((container, type) => new SimpleService());
    }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task should_deduplicate_across_multiple_call_sites()
    {
        var source = @"
using OneShot;

public class SimpleService { }

public class Setup
{
    void A(Container c) { c.Register<SimpleService>(); }
    void B(Container c) { c.Register<SimpleService>(); }
    void C(Container c) { c.Instantiate<SimpleService>(); }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
    }

    [Test]
    public async Task should_generate_for_inject_fields_and_properties()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public Dep FieldDep;
    [Inject] public Dep PropDep { get; set; }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("target.FieldDep = container.Resolve<global::Dep>()");
        await Assert.That(text).Contains("target.PropDep = container.Resolve<global::Dep>()");
    }

    [Test]
    public async Task should_generate_for_inject_method()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public void Init(Dep dep) { }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("target.Init(");
        await Assert.That(text).Contains("container.Resolve<global::Dep>()");
    }

    [Test]
    public async Task should_handle_labeled_parameters()
    {
        var source = @"
using OneShot;

public class Dep { }
public interface MyLabel : ILabel<Dep> { }
public class Service
{
    [Inject] public Service([Inject(typeof(MyLabel))] Dep dep) { }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("typeof(global::MyLabel)");
    }

    [Test]
    public async Task should_handle_default_parameters()
    {
        var source = @"
using OneShot;

public class Dep { }

public class Service
{
    public Dep Dep { get; }
    public int Value { get; }
    public Service(Dep dep, int value = 42) { Dep = dep; Value = value; }
}

public class Setup
{
    void Configure(Container c) { c.Register<Service>(); }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("container.Resolve<global::Dep>()");
        await Assert.That(text).Contains("TryResolve");
        await Assert.That(text).Contains("42");
    }

    [Test]
    public async Task should_skip_private_nested_types()
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
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task should_skip_abstract_types()
    {
        var source = @"
using OneShot;

public abstract class AbstractService { }

public class Setup
{
    void Configure(Container c) { c.Register<AbstractService>(); }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task should_generate_unsafe_accessor_for_private_setter()
    {
        var source = @"
using OneShot;

public class Dep { }
public class Service
{
    [Inject] public Dep Prop { get; private set; }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("UnsafeAccessor");
        await Assert.That(text).Contains("UnsafeSet_Prop");
    }

    [Test]
    public async Task should_include_interfaces_and_base_types()
    {
        var source = @"
using OneShot;

public interface IService { }
public class BaseService { }
public class Service : BaseService, IService
{
    [Inject] public Service() { }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        var text = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(text).Contains("typeof(global::IService)");
        await Assert.That(text).Contains("typeof(global::BaseService)");
    }

    [Test]
    public async Task should_not_generate_for_types_without_inject()
    {
        var source = @"
public class PlainService
{
    public PlainService(int value) { }
}";
        var result = await RunGenerator(source);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }
}
