using OneShot;
using UnityEngine;

public class FooInstaller : MonoBehaviour, IInstaller
{
    public void Install(Container container)
    {
        container.RegisterInstance(123).AsSelf();
        container.RegisterInstance(123f).AsSelf();
    }
}
