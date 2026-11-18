using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DatabaseMaster : MonoBehaviour
{
    public static DatabaseMaster Instance { get; private set; }

    public List<ItemModel> GlobalItemsDatabase { get; private set; } = new List<ItemModel>();

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

    // Carrega o catálogo global de itens do Supabase para a memória do jogo
    public async Task LoadGlobalGameDataAsync()
    {
        try
        {
            var client = SupabaseManager.Instance.Client;

            // Busca todos os itens cadastrados no banco
            var response = await client.From<ItemModel>().Get();
            GlobalItemsDatabase = response.Models;

            Debug.Log($"[DatabaseMaster] {GlobalItemsDatabase.Count} itens carregados do Supabase com sucesso!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DatabaseMaster] Erro ao carregar dados globais do banco: {ex.Message}");
        }
    }
}

// Modelo de Mapeamento da Tabela 'items'
[Supabase.Postgrest.Attributes.Table("items")]
public class ItemModel : Supabase.Postgrest.Models.BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("id")]
    public string Id { get; set; }

    [Supabase.Postgrest.Attributes.Column("item_key")]
    public string ItemKey { get; set; }

    [Supabase.Postgrest.Attributes.Column("display_name")]
    public string DisplayName { get; set; }

    [Supabase.Postgrest.Attributes.Column("item_type")]
    public string ItemType { get; set; }

    [Supabase.Postgrest.Attributes.Column("damage_bonus")]
    public float DamageBonus { get; set; }

    [Supabase.Postgrest.Attributes.Column("defense_bonus")]
    public float DefenseBonus { get; set; }

    [Supabase.Postgrest.Attributes.Column("rarity")]
    public string Rarity { get; set; }
}