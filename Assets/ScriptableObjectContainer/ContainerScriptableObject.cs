using OneShot;
using UnityEngine;

[CreateAssetMenu(fileName = "Container", menuName = "OneShot/Container", order = 0)]
public class ContainerScriptableObject : ScriptableObject
{
    public Container Value { get; } = new Container();
}