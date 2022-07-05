using OneShot;
using UnityEngine;

public class FloatInstaller : MonoBehaviour
{
    [SerializeField] private ContainerScriptableObject _container;
    [SerializeField] private float _value;
    private void Awake() => _container.Value.RegisterInstance(_value).AsSelf();
}
