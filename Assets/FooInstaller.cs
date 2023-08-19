using OneShot;
using UnityEngine;

public class Foo {}
public class FooInstaller : MonoBehaviour, IInstaller
{
    public void Install(Container container)
    {
        container.RegisterInstance(123).AsSelf();
        container.RegisterInstance(123f).AsSelf();
        container.Register<Foo>().Transient().AsSelf();
        container.Register<Foo>().Singleton().AsSelf();
        container.Register<Foo>().Scope().AsSelf();
        container.Register<Foo>().Scope().AsSelf();
    }
}
