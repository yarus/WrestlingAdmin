using System;
using System.Collections.Generic;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Utils
{
    public sealed class DiContainer : IDiContainer
    {
        private static volatile DiContainer _instance;
        private static readonly object SyncRoot = new Object();
        private readonly Dictionary<string, object> _container;

        private DiContainer()
        {
            _container = new Dictionary<string, object>();
        }

        public void Add<T>(object item) where T : class
        {
            Add(item, typeof(T).FullName);
        }

        public void Add(object item, string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ApplicationException("Key for IoC container should not be empty!");

            if (_container.ContainsKey(key))
            {
                throw new ApplicationException("Cant add item to container since such type already exists.");
            }

            _container.Add(key, item);
        }

        public void Remove<T>() where T : class
        {
            var fullName = typeof(T).FullName;

            if (fullName == null) throw new ApplicationException("Can't get type name for IoC container!");

            Remove(fullName);
        }

        public void Remove(string key)
        {
            if (_container.ContainsKey(key))
            {
                _container.Remove(key);
            }
        }

        public T Resolve<T>() where T : class
        {
            var fullName = typeof(T).FullName;

            if (fullName == null) return null;

            return Resolve(fullName) as T;
        }

        public object Resolve(string key)
        {
            if (string.IsNullOrEmpty(key) || !_container.ContainsKey(key)) return null;

            return _container[key];
        }

        public static DiContainer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new DiContainer();
                        }
                    }
                }

                return _instance;
            }
        }
    }
}