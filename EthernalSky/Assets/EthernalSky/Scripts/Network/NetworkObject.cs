using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    [Header("Network Identity")]
    [SerializeField] private string networkId;
    [SerializeField] private bool isOwner;

    public string NetworkId => networkId;
    public bool IsOwner => isOwner;

    public virtual void Initialize(string id, bool owner)
    {
        networkId = id;
        isOwner = owner;
        
        Debug.Log($"[NetworkObject] Inicializado - ID: {networkId} | Dono: {isOwner}");
    }

    public virtual void SyncState()
    {
        if (!isOwner) return;
        // Ponto de extensão para envio de atualizações de posição e estado
    }
}