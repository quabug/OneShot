#nullable enable

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OneShot
{
    public sealed class ContainerComponent : MonoBehaviour
    {
        public Container Value { get; set; } = default!;
        private void OnDestroy() => Value.Dispose();
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(ContainerComponent))]
    public sealed class ContainerComponentDrawer : Editor
    {
        private ContainerComponent _container = default!;
 
        private void OnEnable() {
            _container = (ContainerComponent) target;
        }
 
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            using var _ = new EditorGUI.DisabledScope(true);
            var types = _container.Value.Resolvers.Keys;
            foreach (var type in types)
            {
                EditorGUILayout.LabelField(type.Name);
            }
        }
    }
#endif
}