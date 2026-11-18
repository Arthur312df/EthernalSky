using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "EthernalSky/Database/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [System.Serializable]
    public struct ItemDefinition
    {
        public string itemKey;
        public string displayName;
        public Sprite icon;
        public GameObject worldPrefab; // Prefab opcional para o item cair no chão
    }

    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    public ItemDefinition GetItem(string key)
    {
        foreach (var item in items)
        {
            if (item.itemKey == key) return item;
        }
        return new ItemDefinition { itemKey = key, displayName = "Item Desconhecido" };
    }
}