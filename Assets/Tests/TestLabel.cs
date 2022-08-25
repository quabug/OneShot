using NUnit.Framework;

namespace OneShot.Test
{
    public class TestLabel
    {
        class Foo {}
        interface LabelFoo : ILabel<Foo> {}
        interface LabelAny<T> : ILabel<T> {}

        class Bar
        {
            public Foo LabeledFoo;
            public Foo Foo;
            public Foo AnyLabeledFoo;
            
            public Bar([Inject(typeof(LabelFoo))] Foo labeledFoo, Foo foo, [Inject(typeof(LabelAny<>))] Foo anyLabeledFoo)
            {
                LabeledFoo = labeledFoo;
                Foo = foo;
                AnyLabeledFoo = anyLabeledFoo;
            }
        }

        [Test]
        public void should_make_instance_by_label()
        {
            var container = new Container();
            
            var foo = new Foo();
            var labeledFoo = new Foo();
            var anyLabeledFoo = new Foo();
            
            container.RegisterInstance(foo).AsSelf();
            container.RegisterInstance(labeledFoo).AsSelf(typeof(LabelFoo));
            container.RegisterInstance(anyLabeledFoo).AsSelf(typeof(LabelAny<>));
            
            Assert.That(container.Resolve<Foo>(), Is.SameAs(foo));
            Assert.That(container.Resolve<Foo>(typeof(LabelFoo)), Is.SameAs(labeledFoo));
            Assert.That(container.Resolve<Foo>(typeof(LabelAny<>)), Is.SameAs(anyLabeledFoo));
            
            container.Register<Bar>().AsSelf();
            var bar = container.Resolve<Bar>();
            Assert.That(bar.Foo, Is.SameAs(foo));
            Assert.That(bar.LabeledFoo, Is.SameAs(labeledFoo));
            Assert.That(bar.AnyLabeledFoo, Is.SameAs(anyLabeledFoo));
        }
        
        [Test]
        public void should_make_instance_by_labeled_additional_instances()
        {
            var container = new Container();
            var foo = new Foo();
            var labeledFoo = new Foo();
            var anyLabeledFoo = new Foo();
            
            container.Register<Bar>().With((foo, null), (labeledFoo, typeof(LabelFoo)), (anyLabeledFoo, typeof(LabelAny<>))).AsSelf();
            var bar = container.Resolve<Bar>();
            Assert.That(bar.Foo, Is.SameAs(foo));
            Assert.That(bar.LabeledFoo, Is.SameAs(labeledFoo));
            Assert.That(bar.AnyLabeledFoo, Is.SameAs(anyLabeledFoo));
        }
    }
}
