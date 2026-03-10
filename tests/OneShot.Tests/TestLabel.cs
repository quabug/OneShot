namespace OneShot.Test
{
    public class TestLabel
    {
        internal class Foo {}
        internal interface LabelFoo : ILabel<Foo> {}
        internal interface LabelAny<T> : ILabel<T> {}

        internal class Bar
        {
            public Foo LabeledFoo;
            public Foo Foo;
            public Foo AnyLabeledFoo;

            [Inject(typeof(LabelAny<>))] public Foo AnyProperty { get; private set; }
            [Inject(typeof(LabelFoo))] public Foo FooField;

            public Foo MethodFoo;
            [Inject] public void Inject([Inject(typeof(LabelFoo))] Foo foo) => MethodFoo = foo;

            public Bar([Inject(typeof(LabelFoo))] Foo labeledFoo, Foo foo, [Inject(typeof(LabelAny<>))] Foo anyLabeledFoo)
            {
                LabeledFoo = labeledFoo;
                Foo = foo;
                AnyLabeledFoo = anyLabeledFoo;
            }
        }

        [Test]
        public async Task should_make_instance_by_label()
        {
            var container = new Container();

            var foo = new Foo();
            var labeledFoo = new Foo();
            var anyLabeledFoo = new Foo();

            container.RegisterInstance(foo).AsSelf();
            container.RegisterInstance(labeledFoo).AsSelf(typeof(LabelFoo));
            container.RegisterInstance(anyLabeledFoo).AsSelf(typeof(LabelAny<>));

            await Assert.That(container.Resolve<Foo>()).IsSameReferenceAs(foo);
            await Assert.That(container.Resolve<Foo>(typeof(LabelFoo))).IsSameReferenceAs(labeledFoo);
            await Assert.That(container.Resolve<Foo>(typeof(LabelAny<>))).IsSameReferenceAs(anyLabeledFoo);

            container.Register<Bar>().AsSelf();
            var bar = container.Resolve<Bar>();
            await Assert.That(bar.Foo).IsSameReferenceAs(foo);
            await Assert.That(bar.LabeledFoo).IsSameReferenceAs(labeledFoo);
            await Assert.That(bar.AnyLabeledFoo).IsSameReferenceAs(anyLabeledFoo);
        }

        [Test]
        public async Task should_make_instance_by_labeled_additional_instances()
        {
            var container = new Container();
            var foo = new Foo();
            var labeledFoo = new Foo();
            var anyLabeledFoo = new Foo();

            container.Register<Bar>().With((foo, null), (labeledFoo, typeof(LabelFoo)), (anyLabeledFoo, typeof(LabelAny<>))).AsSelf();
            var bar = container.Resolve<Bar>();
            await Assert.That(bar.Foo).IsSameReferenceAs(foo);
            await Assert.That(bar.LabeledFoo).IsSameReferenceAs(labeledFoo);
            await Assert.That(bar.AnyLabeledFoo).IsSameReferenceAs(anyLabeledFoo);
        }

        [Test]
        public async Task should_inject_labeled_instances()
        {
            var container = new Container();

            var foo = new Foo();
            var labeledFoo = new Foo();
            var anyLabeledFoo = new Foo();

            container.RegisterInstance(foo).AsSelf();
            container.RegisterInstance(labeledFoo).AsSelf(typeof(LabelFoo));
            container.RegisterInstance(anyLabeledFoo).AsSelf(typeof(LabelAny<>));

            container.Register<Bar>().AsSelf();
            var bar = container.Resolve<Bar>();
            await Assert.That(bar.MethodFoo).IsNull();
            await Assert.That(bar.AnyProperty).IsNull();
            await Assert.That(bar.FooField).IsNull();
            container.InjectAll(bar);
            await Assert.That(bar.MethodFoo).IsSameReferenceAs(labeledFoo);
            await Assert.That(bar.AnyProperty).IsSameReferenceAs(anyLabeledFoo);
            await Assert.That(bar.FooField).IsSameReferenceAs(labeledFoo);
        }
    }
}
