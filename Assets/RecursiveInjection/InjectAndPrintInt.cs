using OneShot;
using UnityEngine;

public class InjectAndPrintInt : MonoBehaviour
{
    [Inject]
    void Inject(int value) => Debug.Log($"value = {value}");
}
