using System;
using UnityEngine;

public sealed class ResourceHandle<T> : IDisposable where T : UnityEngine.Object
{
    private readonly string _address;
    private readonly T _asset;
    private readonly Action _release;
    private bool _disposed;

    internal ResourceHandle(string address, T asset, Action release)
    {
        _address = address;
        _asset = asset;
        _release = release;
    }

    public string Address => _address;
    public T Asset => _disposed ? null : _asset;
    public bool IsValid => !_disposed && _asset != null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _release?.Invoke();
    }
}
