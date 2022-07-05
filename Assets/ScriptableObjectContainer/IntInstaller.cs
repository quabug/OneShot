using OneShot;
using UnityEngine;

public class IntInstaller : MonoBehaviour
{
    [SerializeField] private ContainerScriptableObject _container;
    [SerializeField] private int _value;
    private void Awake() => _container.Value.RegisterInstance(_value).AsSelf();
}
