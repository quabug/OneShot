#nullable enable

using System;
using System.Linq;
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
            foreach (var (type, resolverStack) in _container.Value.Resolvers)
            {
                var label = $"{type.Name}({resolverStack.Count})";
                var resolversText = resolverStack.Select(resolver => resolver.Lifetime).Select(lifetime => lifetime switch
                {
                    Lifetime.Singleton => "1",
                    Lifetime.Transient => "*",
                    Lifetime.Scope => "<1>",
                    _ => throw new NotSupportedException()
                });
                EditorGUILayout.TextField(label, string.Join("|", resolversText));
            }
        }
    }
#endif
}