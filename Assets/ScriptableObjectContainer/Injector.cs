using OneShot;
using UnityEngine;

public class Injector : MonoBehaviour
{
    [SerializeField] private ContainerScriptableObject _container;

    private void Start()
    {
        foreach (var component in GetComponents<MonoBehaviour>()) _container.Value.InjectAll(component);
    }
}