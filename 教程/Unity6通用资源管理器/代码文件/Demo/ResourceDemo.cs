using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceDemo : MonoBehaviour
{
    [SerializeField] private string spriteAddress = "Item/Icon/1001";
    [SerializeField] private Image target;
    private ResourceHandle<Sprite> _spriteHandle;

    private async UniTaskVoid Start()
    {
        try
        {
            _spriteHandle = await ResourceManager.Instance.LoadHandleAsync<Sprite>(spriteAddress, destroyCancellationToken);
            if (this == null || !isActiveAndEnabled || _spriteHandle == null)
            {
                return;
            }

            if (target != null)
            {
                target.sprite = _spriteHandle.Asset;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDestroy()
    {
        _spriteHandle?.Dispose();
        _spriteHandle = null;
    }
}
