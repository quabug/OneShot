using OneShot;
using UnityEngine;

public class InjectAndPrint : MonoBehaviour
{
    [Inject] void Inject(int intValue, float floatValue) => Debug.Log($"int = {intValue}, float = {floatValue}");
}