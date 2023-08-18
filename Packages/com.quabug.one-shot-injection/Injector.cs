#nullable enable

using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace OneShot
{
    public interface IInjectable
    {
    }

    public interface IInstaller
    {
        void Install(Container container);
    }

    [DisallowMultipleComponent]
    public sealed class Injector : MonoBehaviour
    {
        public enum Phase { Awake, Start, Update, LateUpdate, Manual }

        [field: SerializeField] public Phase InjectPhase = Phase.Start;
        [field: SerializeField] public bool _injectableOnly = false;
        [field: SerializeField] public bool _recursive = true;
        [field: SerializeField] public bool _stopOnInactiveObject = false;

        private void Awake()
        {
            if (InjectPhase == Phase.Awake) Inject();
        }

        private void Start()
        {
            if (InjectPhase == Phase.Start) Inject();
        }

        private void Update()
        {
            if (InjectPhase == Phase.Update) Inject();
        }

        private void LateUpdate()
        {
            if (InjectPhase == Phase.LateUpdate) Inject();
        }

        public void Inject(Container container)
        {
            Debug.Assert(!enabled, "Inject already called");
            enabled = false;
            if (_injectableOnly) container.RecursiveInjectAll<IInjectable>(gameObject, _stopOnInactiveObject);
            else container.RecursiveInjectAll<MonoBehaviour>(gameObject, _stopOnInactiveObject);
        }

        private void Inject()
        {
            var container = new Container();
            container.RegisterInstance(container).AsSelf();
            gameObject.AddComponent<ContainerComponent>().Value = container;
            Inject(container);
        }
    }

    public static class RecursiveInjectorExtension
    {
        public static void InjectScene(this Container container, Scene scene)
        {
            var roots = ListPool<GameObject>.Get();
            try
            {
                scene.GetRootGameObjects(roots);
                foreach (var obj in roots)
                {
                    if (obj.TryGetComponent<Injector>(out var injector) && injector.InjectPhase == Injector.Phase.Manual)
                    {
                        injector.Inject(container);
                    }
                }
            }
            finally
            {
                ListPool<GameObject>.Release(roots);
            }
        }


        public static void InjectGameObject<T>(this Container container, GameObject gameObject)
        {
            var components = ListPool<T>.Get();
            try
            {
                gameObject.GetComponents(components);
                foreach (var component in components) container.InjectAll(component);
            }
            finally
            {
                ListPool<T>.Release(components);
            }
        }

        public static void TryInstallGameObject(this Container container, GameObject self, ContainerComponent? parent = null)
        {
            var installers = ListPool<IInstaller>.Get();
            try
            {
                self.GetComponents(installers);
                foreach (var installer in installers) container.InjectAll(installer);

                if (installers.Any())
                {
                    container = container.CreateChildContainer();
                    container.RegisterInstance(container).AsSelf();
                    var containerComponent = self.AddComponent<ContainerComponent>();
                    containerComponent.Value = container;
                    containerComponent.Parent = parent;
                }

                foreach (var installer in installers) installer.Install(container);
            }
            finally
            {
                ListPool<IInstaller>.Release(installers);
            }
        }

        private static void InstallAndInjectGameObject<T>(this Container container, GameObject self, ContainerComponent? parent = null)
        {
            container.TryInstallGameObject(self, parent);
            container.InjectGameObject<T>(self);
        }

        public static void RecursiveInjectAll<T>(this Container container, GameObject self, bool stopOnInactiveObject)
        {
            // skip inactive object and its children
            if (stopOnInactiveObject && !self.activeInHierarchy) return;
            if (self.TryGetComponent<StopInjection>(out _)) return;

            container.InstallAndInjectGameObject<T>(self);

            for (var i = 0; i < self.transform.childCount; i++)
                container.RecursiveInjectAll<T>(self.transform.GetChild(i).gameObject, stopOnInactiveObject);
        }
    }
}
