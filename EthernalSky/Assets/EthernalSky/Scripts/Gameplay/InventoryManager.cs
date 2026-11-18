using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EthernalSky.Models;

public enum EquipmentSlotType
{
    None = 0,
    Helmet = 1,
    Chest = 2,
    Weapon = 3,
    Gloves = 4,
    Pants = 5,
    Boots = 6
}

public class ItemMetadata
{
    public string DisplayName;
    public EquipmentSlotType SlotType;
    public float BonusDamage;
    public float BonusHp;
    public bool IsStackable;

    public ItemMetadata(string displayName, EquipmentSlotType slotType, float bonusDamage, float bonusHp, bool isStackable = false)
    {
        DisplayName = displayName;
        SlotType = slotType;
        BonusDamage = bonusDamage;
        BonusHp = bonusHp;
        IsStackable = isStackable;
    }
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public List<InventoryItemModel> PlayerInventory { get; private set; } = new List<InventoryItemModel>();
    public event Action OnInventoryChanged;

    public float TotalBonusDamage { get; private set; } = 0f;
    public float TotalBonusHp { get; private set; } = 0f;
    public int TotalGold { get; private set; } = 0;

    private bool isLoaded = false;
    private string activeCharacterId;

    private readonly Dictionary<string, ItemMetadata> itemCatalog = new Dictionary<string, ItemMetadata>
    {
        { "iron_dagger", new ItemMetadata("Adaga de Ferro", EquipmentSlotType.Weapon, 15f, 0f, false) },
        { "shadow_dagger", new ItemMetadata("Adaga das Sombras", EquipmentSlotType.Weapon, 28f, 0f, false) },
        { "iron_helmet", new ItemMetadata("Capacete de Ferro", EquipmentSlotType.Helmet, 0f, 25f, false) },
        { "iron_chest", new ItemMetadata("Peitoral de Ferro", EquipmentSlotType.Chest, 0f, 50f, false) },
        { "leather_gloves", new ItemMetadata("Luvas de Couro", EquipmentSlotType.Gloves, 5f, 15f, false) },
        { "chain_pants", new ItemMetadata("Calça de Malha", EquipmentSlotType.Pants, 0f, 35f, false) },
        { "leather_boots", new ItemMetadata("Botas de Couro", EquipmentSlotType.Boots, 0f, 20f, false) },

        { "health_potion", new ItemMetadata("Poção de Vida", EquipmentSlotType.None, 0f, 0f, true) },
        { "mana_potion", new ItemMetadata("Poção de Mana", EquipmentSlotType.None, 0f, 0f, true) },
        { "gold_coin", new ItemMetadata("Moedas de Ouro", EquipmentSlotType.None, 0f, 0f, true) }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializePlayerInventoryAsync();
    }

    public ItemMetadata GetItemMetadata(string itemKey)
    {
        if (!string.IsNullOrEmpty(itemKey) && itemCatalog.TryGetValue(itemKey, out ItemMetadata meta))
        {
            return meta;
        }
        return new ItemMetadata(itemKey, EquipmentSlotType.Weapon, 5f, 0f, false);
    }

    public async Task InitializePlayerInventoryAsync()
    {
        string characterId = GetActiveCharacterId();

        if (!string.IsNullOrEmpty(characterId))
        {
            activeCharacterId = characterId;
            await LoadInventoryAsync(characterId);
        }
    }

    private string GetActiveCharacterId()
    {
        if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
        {
            return CharacterDataManager.Instance.CurrentCharacter.Id;
        }
        return activeCharacterId;
    }

    public async Task LoadInventoryAsync(string characterId)
    {
        try
        {
            var client = SupabaseManager.Instance.Client;
            var response = await client.From<InventoryItemModel>()
                .Where(x => x.CharacterId == characterId)
                .Get();

            PlayerInventory = response.Models;
            isLoaded = true;
            activeCharacterId = characterId;

            await NormalizeNonStackableItemsAsync(characterId);

            RecalculateEquipmentBonuses();
            Debug.Log($"[InventoryManager] Inventário sincronizado! {PlayerInventory.Count} registro(s) carregados.");
            OnInventoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryManager] Erro ao carregar inventário: {ex.Message}");
        }
    }

    private async Task NormalizeNonStackableItemsAsync(string characterId)
    {
        var client = SupabaseManager.Instance.Client;
        List<InventoryItemModel> snapshot = new List<InventoryItemModel>(PlayerInventory);

        foreach (var item in snapshot)
        {
            if (item == null) continue;

            ItemMetadata meta = GetItemMetadata(item.ItemKey);
            if (!meta.IsStackable && item.Quantity > 1)
            {
                int extraCopies = item.Quantity - 1;
                item.Quantity = 1;
                await client.From<InventoryItemModel>().Update(item);

                for (int i = 0; i < extraCopies; i++)
                {
                    var splitItem = new InventoryItemModel
                    {
                        CharacterId = characterId,
                        ItemKey = item.ItemKey,
                        Quantity = 1,
                        IsEquipped = false
                    };

                    var insertResp = await client.From<InventoryItemModel>().Insert(splitItem);
                    if (insertResp.Models.Count > 0)
                    {
                        PlayerInventory.Add(insertResp.Models[0]);
                    }
                }
            }
        }
    }

    public async Task<bool> AddItemAsync(string characterId, string itemKey, int amount = 1)
    {
        try
        {
            if (!isLoaded || activeCharacterId != characterId)
            {
                await LoadInventoryAsync(characterId);
            }

            var client = SupabaseManager.Instance.Client;
            ItemMetadata meta = GetItemMetadata(itemKey);

            if (meta.IsStackable)
            {
                var existingItem = PlayerInventory.Find(x => x.ItemKey == itemKey && !x.IsEquipped);
                if (existingItem != null)
                {
                    existingItem.Quantity += amount;
                    await client.From<InventoryItemModel>().Update(existingItem);

                    RecalculateEquipmentBonuses();
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                else
                {
                    var newStack = new InventoryItemModel
                    {
                        CharacterId = characterId,
                        ItemKey = itemKey,
                        Quantity = amount,
                        IsEquipped = false
                    };

                    var response = await client.From<InventoryItemModel>().Insert(newStack);
                    if (response.Models.Count > 0)
                    {
                        PlayerInventory.Add(response.Models[0]);
                        RecalculateEquipmentBonuses();
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
            else
            {
                bool anyInserted = false;
                for (int i = 0; i < amount; i++)
                {
                    var singleEquipment = new InventoryItemModel
                    {
                        CharacterId = characterId,
                        ItemKey = itemKey,
                        Quantity = 1,
                        IsEquipped = false
                    };

                    var response = await client.From<InventoryItemModel>().Insert(singleEquipment);
                    if (response.Models.Count > 0)
                    {
                        PlayerInventory.Add(response.Models[0]);
                        anyInserted = true;
                    }
                }

                if (anyInserted)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryManager] Erro ao adicionar item: {ex.Message}");
            return false;
        }
    }

    public async void EquipItemAsync(InventoryItemModel itemToEquip)
    {
        if (itemToEquip == null || itemToEquip.IsEquipped) return;

        ItemMetadata targetMeta = GetItemMetadata(itemToEquip.ItemKey);
        if (targetMeta.SlotType == EquipmentSlotType.None)
        {
            Debug.Log($"[Inventário] '{targetMeta.DisplayName}' não pode ser equipado no corpo.");
            return;
        }

        try
        {
            var client = SupabaseManager.Instance.Client;

            foreach (var invItem in PlayerInventory)
            {
                if (invItem.IsEquipped && GetItemMetadata(invItem.ItemKey).SlotType == targetMeta.SlotType)
                {
                    invItem.IsEquipped = false;
                    await client.From<InventoryItemModel>().Update(invItem);
                }
            }

            itemToEquip.IsEquipped = true;
            await client.From<InventoryItemModel>().Update(itemToEquip);

            RecalculateEquipmentBonuses();
            Debug.Log($"[Equipamento] '{targetMeta.DisplayName}' equipado no slot {targetMeta.SlotType}!");
            OnInventoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryManager] Erro ao equipar item: {ex.Message}");
        }
    }

    public async void UnequipItemAsync(InventoryItemModel itemToUnequip)
    {
        if (itemToUnequip == null || !itemToUnequip.IsEquipped) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            itemToUnequip.IsEquipped = false;
            await client.From<InventoryItemModel>().Update(itemToUnequip);

            RecalculateEquipmentBonuses();
            Debug.Log($"[Equipamento] Item '{itemToUnequip.ItemKey}' devolvido para a mochila.");
            OnInventoryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryManager] Erro ao desequipar item: {ex.Message}");
        }
    }

    // Remove o item do inventário (Supabase + memória) e joga fisicamente no chão da cena
    public async void DropItemToWorldAsync(InventoryItemModel itemToDrop)
    {
        if (itemToDrop == null) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            ItemMetadata meta = GetItemMetadata(itemToDrop.ItemKey);

            // Remove do banco de dados
            await client.From<InventoryItemModel>().Delete(itemToDrop);
            PlayerInventory.Remove(itemToDrop);

            RecalculateEquipmentBonuses();
            OnInventoryChanged?.Invoke();

            // Spawna o objeto físico no chão ao lado do Player
            SpawnPhysicalDropInWorld(itemToDrop.ItemKey, meta.DisplayName, itemToDrop.Quantity);
            Debug.Log($"[Inventário] '{meta.DisplayName}' foi jogado para fora do inventário e caiu no chão!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryManager] Erro ao descartar item para o chão: {ex.Message}");
        }
    }

    private void SpawnPhysicalDropInWorld(string itemKey, string displayName, int count)
    {
        var player = FindAnyObjectByType<IsometricPlayerController>();
        Vector3 basePos = player != null ? player.transform.position : Vector3.zero;

        int spawnCount = Mathf.Clamp(count, 1, 5);
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.2f, 2.0f);
            Vector3 dropPos = new Vector3(basePos.x + randomCircle.x, 0.5f, basePos.z + randomCircle.y);

            if (Physics.Raycast(new Vector3(dropPos.x, basePos.y + 5f, dropPos.z), Vector3.down, out RaycastHit hit, 20f))
            {
                dropPos.y = hit.point.y + 0.12f;
            }

            GameObject dropObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dropObj.transform.position = dropPos;
            dropObj.transform.localScale = new Vector3(0.45f, 0.12f, 0.9f);
            dropObj.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            if (dropObj.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            }

            if (dropObj.TryGetComponent<BoxCollider>(out var boxCol))
            {
                // Aumenta a área de clique para facilitar pegar de volta do chão
                boxCol.size = new Vector3(2.2f, 3.0f, 2.2f);
            }

            WorldItemDrop dropScript = dropObj.AddComponent<WorldItemDrop>();
            dropScript.ConfigureDrop(itemKey, displayName);
        }
    }

    public InventoryItemModel GetEquippedItemInSlot(EquipmentSlotType slotType)
    {
        foreach (var item in PlayerInventory)
        {
            if (item != null && item.IsEquipped && GetItemMetadata(item.ItemKey).SlotType == slotType)
            {
                return item;
            }
        }
        return null;
    }

    private void RecalculateEquipmentBonuses()
    {
        TotalBonusDamage = 0f;
        TotalBonusHp = 0f;
        TotalGold = 0;

        foreach (var item in PlayerInventory)
        {
            if (item == null) continue;

            if (item.ItemKey == "gold_coin")
            {
                TotalGold += item.Quantity;
                continue;
            }

            if (item.IsEquipped)
            {
                ItemMetadata meta = GetItemMetadata(item.ItemKey);
                TotalBonusDamage += meta.BonusDamage;
                TotalBonusHp += meta.BonusHp;
            }
        }
    }
}