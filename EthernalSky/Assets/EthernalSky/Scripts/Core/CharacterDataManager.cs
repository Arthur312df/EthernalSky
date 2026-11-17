using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance { get; private set; }

    [Header("Credenciais de Teste (Dev)")]
    [SerializeField] private string testEmail = "dev@ethernalsky.com";
    [SerializeField] private string testPassword = "Password123!";
    [SerializeField] private string testUsername = "ArthurDev";
    [SerializeField] private string defaultCharacterName = "Kael";

    public ProfileModel CurrentProfile { get; private set; }
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

    [ContextMenu("1. Autenticar e Carregar Dados")]
    public async void RunTestFlow()
    {
        await AuthenticateAndInitializeAsync(testEmail, testPassword, testUsername, defaultCharacterName);
    }

    [ContextMenu("2. Salvar Posição do Personagem")]
    public async void TestSavePosition()
    {
        if (CurrentCharacter == null)
        {
            Debug.LogWarning("[CharacterDataManager] Nenhum personagem carregado para salvar.");
            return;
        }

        await SaveCharacterPositionAsync(transform.position);
    }

    public async Task AuthenticateAndInitializeAsync(string email, string password, string username, string charName)
    {
        try
        {
            var client = SupabaseManager.Instance.Client;

            Debug.Log("[CharacterDataManager] Tentando autenticar usuário...");
            var session = await SignInOrSignUpAsync(email, password);

            if (session == null || session.User == null)
            {
                Debug.LogError("[CharacterDataManager] Não foi possível obter uma sessão ativa. Verifique se 'Confirm email' está desativado no Supabase.");
                return;
            }

            string userId = session.User.Id;
            Debug.Log($"[CharacterDataManager] Usuário autenticado! UID: {userId}");

            // 1. Garantir Perfil na tabela 'profiles'
            CurrentProfile = await GetOrCreateProfileAsync(userId, username);

            // 2. Garantir Personagem na tabela 'characters'
            CurrentCharacter = await GetOrCreateCharacterAsync(userId, charName);

            // 3. Garantir Loadout (1 Ataque Básico + 5 Active Skills) na tabela 'character_skills'
            CurrentSkills = await GetOrCreateDefaultSkillsAsync(CurrentCharacter.Id);

            Debug.Log($"[CharacterDataManager] Tudo pronto! Personagem '{CurrentCharacter.Name}' (Nível {CurrentCharacter.Level}) carregado com {CurrentSkills.Count} slots de habilidade.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterDataManager] Erro no fluxo de dados: {ex.Message}");
        }
    }

    private async Task<Supabase.Gotrue.Session> SignInOrSignUpAsync(string email, string password)
    {
        var client = SupabaseManager.Instance.Client;

        try
        {
            return await client.Auth.SignIn(email, password);
        }
        catch
        {
            Debug.Log("[CharacterDataManager] Conta não encontrada. Criando novo registro no Supabase Auth...");
            return await client.Auth.SignUp(email, password);
        }
    }

    private async Task<ProfileModel> GetOrCreateProfileAsync(string userId, string username)
    {
        var client = SupabaseManager.Instance.Client;

        var response = await client.From<ProfileModel>()
            .Where(x => x.Id == userId)
            .Get();

        if (response.Models.Count > 0)
        {
            Debug.Log($"[CharacterDataManager] Perfil existente carregado: {response.Models[0].Username}");
            return response.Models[0];
        }

        var newProfile = new ProfileModel
        {
            Id = userId,
            Username = username,
            CreatedAt = DateTime.UtcNow
        };

        await client.From<ProfileModel>().Insert(newProfile);
        Debug.Log($"[CharacterDataManager] Novo perfil criado na tabela 'profiles': {username}");
        return newProfile;
    }

    private async Task<CharacterModel> GetOrCreateCharacterAsync(string userId, string characterName)
    {
        var client = SupabaseManager.Instance.Client;

        var response = await client.From<CharacterModel>()
            .Where(x => x.UserId == userId)
            .Get();

        if (response.Models.Count > 0)
        {
            var existingChar = response.Models[0];
            Debug.Log($"[CharacterDataManager] Personagem existente carregado: {existingChar.Name} | Pos: ({existingChar.PosX}, {existingChar.PosY}, {existingChar.PosZ})");
            return existingChar;
        }

        var newChar = new CharacterModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = characterName,
            Level = 1,
            Experience = 0,
            CurrentHp = 100f,
            MaxHp = 100f,
            CurrentMana = 50f,
            MaxMana = 50f,
            PosX = 0f,
            PosY = 0f,
            PosZ = 0f,
            CreatedAt = DateTime.UtcNow
        };

        await client.From<CharacterModel>().Insert(newChar);
        Debug.Log($"[CharacterDataManager] Novo personagem registrado no banco: {newChar.Name} (ID: {newChar.Id})");
        return newChar;
    }

    private async Task<List<CharacterSkillModel>> GetOrCreateDefaultSkillsAsync(string characterId)
    {
        var client = SupabaseManager.Instance.Client;

        var response = await client.From<CharacterSkillModel>()
            .Where(x => x.CharacterId == characterId)
            .Get();

        if (response.Models.Count > 0)
        {
            return response.Models;
        }

        // Estrutura padrão do EthernalSky: 1 Ataque Básico + 5 Skills Ativas
        var defaultSkills = new List<CharacterSkillModel>
        {
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "basic_slash", SlotType = "basic_attack", SlotIndex = 0 },
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "skill_slot_1", SlotType = "active_1", SlotIndex = 1 },
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "skill_slot_2", SlotType = "active_2", SlotIndex = 2 },
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "skill_slot_3", SlotType = "active_3", SlotIndex = 3 },
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "skill_slot_4", SlotType = "active_4", SlotIndex = 4 },
            new CharacterSkillModel { Id = Guid.NewGuid().ToString(), CharacterId = characterId, SkillKey = "skill_slot_5", SlotType = "active_5", SlotIndex = 5 }
        };

        await client.From<CharacterSkillModel>().Insert(defaultSkills);
        Debug.Log("[CharacterDataManager] Loadout padrão (1 ataque básico + 5 skills) gravado em 'character_skills'!");
        return defaultSkills;
    }

    public async Task SaveCharacterPositionAsync(Vector3 position)
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.PosX = position.x;
        CurrentCharacter.PosY = position.y;
        CurrentCharacter.PosZ = position.z;

        await SupabaseManager.Instance.Client
            .From<CharacterModel>()
            .Update(CurrentCharacter);

        Debug.Log($"[CharacterDataManager] Posição salva no Supabase: ({position.x:F2}, {position.y:F2}, {position.z:F2})");
    }
}