using System;
using System.Collections.Generic;
using Wrestling.UI.Utils;

namespace Wrestling.UI.Material.Tests.Fakes;

// Isolated DI container for VM tests — independent from the app's static
// DiContainer.Instance, so tests never share state through the singleton.
public sealed class TestDiContainer : IDiContainer
{
    private readonly Dictionary<string, object> _map = new();

    public T Resolve<T>() where T : class
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        return _map.TryGetValue(key, out var v) ? v as T : null;
    }

    public object Resolve(string key) => _map.TryGetValue(key, out var v) ? v : null;

    public void Add<T>(object item) where T : class
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        _map[key] = item ?? throw new ArgumentNullException(nameof(item));
    }

    public void Add(object item, string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("key required");
        _map[key] = item;
    }

    public void Remove<T>() where T : class
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        _map.Remove(key);
    }

    public void Remove(string key) => _map.Remove(key);
}
