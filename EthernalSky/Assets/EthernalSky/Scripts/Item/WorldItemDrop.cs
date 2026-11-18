using UnityEngine;
using UnityEngine.InputSystem;
using EthernalSky.Models;

[RequireComponent(typeof(Collider))]
public class WorldItemDrop : MonoBehaviour
{
    public static bool IsHoveringAnyItem { get; private set; } = false;

    [Header("Configuração do Item no Chão")]
    [SerializeField] private string itemKey = "iron_dagger";
    [SerializeField] private string displayName = "Adaga de Ferro";
    [SerializeField] private float maxPickupRange = 4.0f;

    private Transform playerTransform;
    private Renderer meshRenderer;
    private Color originalColor;
    private bool isThisItemHovered = false;
    private bool isPickingUp = false;

    public void ConfigureDrop(string key, string friendlyName)
    {
        itemKey = key;
        displayName = friendlyName;
        gameObject.name = $"Drop_{friendlyName}";
    }

    private void Start()
    {
        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }

        var player = FindAnyObjectByType<IsometricPlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (isThisItemHovered && !isPickingUp && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPickUpItem();
        }
    }

    private void OnMouseEnter()
    {
        isThisItemHovered = true;
        IsHoveringAnyItem = true;
        Debug.Log($"[Loot Hover] Item focado: {displayName} ({itemKey})");

        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.yellow;
        }
    }

    private void OnMouseExit()
    {
        isThisItemHovered = false;
        IsHoveringAnyItem = false;

        if (meshRenderer != null)
        {
            meshRenderer.material.color = originalColor;
        }
    }

    private void OnDestroy()
    {
        if (isThisItemHovered)
        {
            IsHoveringAnyItem = false;
        }
    }

    private void TryPickUpItem()
    {
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance > maxPickupRange)
            {
                Debug.Log($"[Loot] Você está muito longe de {displayName} para pegar! Distância: {distance:F1}m");
                return;
            }
        }

        PickUpItem();
    }

    private async void PickUpItem()
    {
        isPickingUp = true;
        string characterId = string.Empty;

        if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
        {
            characterId = CharacterDataManager.Instance.CurrentCharacter.Id;
        }

        if (string.IsNullOrEmpty(characterId))
        {
            try
            {
                var client = SupabaseManager.Instance.Client;
                var response = await client.From<CharacterModel>().Get();
                if (response.Models.Count > 0)
                {
                    characterId = response.Models[0].Id;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Loot] Erro ao buscar personagem de fallback: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(characterId))
        {
            if (InventoryManager.Instance == null)
            {
                GameObject invObj = new GameObject("InventoryManager");
                invObj.AddComponent<InventoryManager>();
            }

            bool saved = await InventoryManager.Instance.AddItemAsync(characterId, itemKey, 1);
            if (saved)
            {
                IsHoveringAnyItem = false;
                Debug.Log($"[Loot] Item coletado e salvo na nuvem com sucesso: {displayName}!");
                Destroy(gameObject);
            }
            else
            {
                isPickingUp = false;
                Debug.LogWarning($"[Loot] Falha ao salvar {displayName} no Supabase.");
            }
        }
        else
        {
            isPickingUp = false;
            Debug.LogWarning("[Loot] Character ID inválido para vincular o item.");
        }
    }
}