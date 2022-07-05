using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace OneShot
{
    public interface IInstaller
    {
        void Install([NotNull] Container container);
    }

    public interface IInjectable {}
    
    [DisallowMultipleComponent]
    public sealed class RecursiveInjector : MonoBehaviour
    {
        enum InjectPhase { Awake, Start, Manual }
        [SerializeField] private InjectPhase _injectPhase = InjectPhase.Start;
        [SerializeField] private bool _injectableOnly = false;

        private void Awake()
        {
            if (_injectPhase == InjectPhase.Awake) Inject();
        }

        private void Start()
        {
            if (_injectPhase == InjectPhase.Start) Inject();
        }

        public void Inject()
        {
            var container = new Container();
            gameObject.AddComponent<ContainerComponent>().Value = container;
            if (_injectableOnly) Inject(gameObject, container, new List<IInstaller>(4), new List<IInjectable>(8));
            else Inject(gameObject, container, new List<IInstaller>(4), new List<MonoBehaviour>(8));
        }

        private void Inject<T>(GameObject self, Container container, List<IInstaller> installers, List<T> components)
        {
            self.GetComponents(installers);
            foreach (var installer in installers) container.InjectAll(installer);
            if (installers.Any())
            {
                container = container.CreateChildContainer();
                self.AddComponent<ContainerComponent>().Value = container;
            }
            foreach (var installer in installers) installer.Install(container);
            
            self.GetComponents(components);
            foreach (var component in components) container.InjectAll(component);

            for (var i = 0; i < self.transform.childCount; i++) Inject(self.transform.GetChild(i).gameObject, container, installers, components);
        }
    }
    
    public sealed class ContainerComponent : MonoBehaviour
    {
        public Container Value { get; set; }
        private void OnDestroy() => Value?.Dispose();
    }
}