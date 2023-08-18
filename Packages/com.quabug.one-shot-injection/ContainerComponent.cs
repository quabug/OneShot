#nullable enable

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OneShot
{
    public sealed class ContainerComponent : MonoBehaviour
    {
        public ContainerComponent? Parent { get; set; }
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
            EditorGUILayout.ObjectField(_container.Parent, typeof(ContainerComponent), allowSceneObjects: true);

            //
            // if(before != after) {
            //     return; //prevent an exception due to Unity doing this in two passes
            // }
            //
            // for (int i = 0; i < script.animals.Count; i++) {
            //     if(script.animals[i] == null) {
            //         EditorGUILayout.LabelField("Null animal");
            //     }
            //     else {
            //         var animal = script.animals[i];
            //         EditorGUILayout.LabelField(animal.name);
            //         EditorGUI.indentLevel++;
            //         FindEditorFor(animal).OnInspectorGUI();
            //         EditorGUI.indentLevel--;
            //     }
            // }
        }
    }
#endif
}