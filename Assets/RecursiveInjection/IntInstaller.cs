using OneShot;
using UnityEngine;

public class IntInstaller : MonoBehaviour, IInstaller
{
    [SerializeField] private int _value;
    
    public void Install(Container container)
    {
        container.RegisterInstance(_value).AsSelf();
    }
}
