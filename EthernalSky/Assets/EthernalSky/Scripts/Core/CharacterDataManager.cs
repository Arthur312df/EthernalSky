using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EthernalSky.Models;
using UnityEngine;

public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance { get; private set; }

    public CharacterModel CurrentCharacter { get; private set; }
    public List<CharacterSkillModel> CurrentSkills { get; private set; } = new List<CharacterSkillModel>();

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

    // Método chamado para carregar os dados da sessão do jogador autenticado
public async Task LoadSessionDataAsync(string email, string password)
    {
        try
        {
            var client = SupabaseManager.Instance.Client;
            Supabase.Gotrue.Session session = null;

            // Tenta fazer o login; se falhar (usuário não existe ou senha inválida), tenta criar a conta
            try
            {
                session = await client.Auth.SignIn(email, password);
            }
            catch (Exception)
            {
                Debug.Log("[CharacterDataManager] Conta não encontrada ou credenciais inválidas. Tentando cadastrar novo usuário...");
                
                // Tenta registrar a nova conta
                await client.Auth.SignUp(email, password);
                
                // Faz o login logo após o cadastro bem-sucedido
                session = await client.Auth.SignIn(email, password);
            }

            if (session?.User == null)
            {
                throw new System.Exception("Não foi possível autenticar o usuário no Supabase.");
            }

            string userId = session.User.Id;

            // 2. Busca o personagem do usuário na tabela 'characters'
            var characterResponse = await client.From<CharacterModel>()
                .Where(x => x.UserId == userId)
                .Get();

            if (characterResponse.Models.Count > 0)
            {
                CurrentCharacter = characterResponse.Models[0];
            }
            else
            {
                // Cria um personagem padrão caso o usuário não tenha nenhum gravado
                CurrentCharacter = new CharacterModel
                {
                    UserId = userId,
                    Name = "Herói de EthernalSky",
                    CurrentHp = 100f,
                    MaxHp = 100f,
                    CurrentMana = 50f,
                    MaxMana = 50f,
                    PosX = 0f,
                    PosY = 1f,
                    PosZ = 0f
                };
                await client.From<CharacterModel>().Insert(CurrentCharacter);
            }

            // 3. Busca as habilidades salvas na tabela 'character_skills'
            var skillsResponse = await client.From<CharacterSkillModel>()
                .Where(x => x.CharacterId == CurrentCharacter.Id)
                .Get();

            CurrentSkills = skillsResponse.Models;

            Debug.Log($"[CharacterDataManager] Sessão carregada com sucesso para: {CurrentCharacter.Name} | Skills vinculadas: {CurrentSkills.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterDataManager] Erro ao carregar dados da sessão: {ex.Message}");
            throw; // Repassa o erro para a UI exibir se necessário
        }
    }

    public async Task SaveCharacterPositionAsync(Vector3 newPosition)
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.PosX = newPosition.x;
        CurrentCharacter.PosY = newPosition.y;
        CurrentCharacter.PosZ = newPosition.z;

        var client = SupabaseManager.Instance.Client;
        await client.From<CharacterModel>()
            .Where(x => x.Id == CurrentCharacter.Id)
            .Update(CurrentCharacter);

        Debug.Log("[CharacterDataManager] Posição do personagem salva no Supabase!");
    }
}

// Modelos de Mapeamento para o Supabase (PostgreSQL)
[Supabase.Postgrest.Attributes.Table("characters")]
public class CharacterModel : Supabase.Postgrest.Models.BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("id")]
    public string Id { get; set; }

    [Supabase.Postgrest.Attributes.Column("user_id")]
    public string UserId { get; set; }

    [Supabase.Postgrest.Attributes.Column("name")]
    public string Name { get; set; }

    [Supabase.Postgrest.Attributes.Column("current_hp")]
    public float CurrentHp { get; set; }

    [Supabase.Postgrest.Attributes.Column("max_hp")]
    public float MaxHp { get; set; }

    [Supabase.Postgrest.Attributes.Column("current_mana")]
    public float CurrentMana { get; set; }

    [Supabase.Postgrest.Attributes.Column("max_mana")]
    public float MaxMana { get; set; }

    [Supabase.Postgrest.Attributes.Column("pos_x")]
    public float PosX { get; set; }

    [Supabase.Postgrest.Attributes.Column("pos_y")]
    public float PosY { get; set; }

    [Supabase.Postgrest.Attributes.Column("pos_z")]
    public float PosZ { get; set; }
}

[Supabase.Postgrest.Attributes.Table("character_skills")]
public class CharacterSkillModel : Supabase.Postgrest.Models.BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("id")]
    public string Id { get; set; }

    [Supabase.Postgrest.Attributes.Column("character_id")]
    public string CharacterId { get; set; }

    [Supabase.Postgrest.Attributes.Column("skill_key")]
    public string SkillKey { get; set; }

    [Supabase.Postgrest.Attributes.Column("slot_index")]
    public int SlotIndex { get; set; }
}