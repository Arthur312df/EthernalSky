using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EthernalSky.Models;

public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    public string CurrentSessionId { get; private set; }
    public bool IsSoloSession { get; private set; } = true;
    public string LocalCharacterId { get; private set; }
    public string LocalCharacterName { get; private set; } = "Herói";

    public List<FriendshipModel> FriendsList { get; private set; } = new List<FriendshipModel>();
    public SessionInviteModel PendingInvite { get; private set; }

    public event Action OnSocialDataChanged;

    private Transform localPlayerTransform;
    private float syncTimer = 0f;
    private float invitePollTimer = 0f;
    private bool isSyncing = false;

    // Representação visual dos amigos conectados na mesma sessão
    private class RemotePlayerAvatar
    {
        public GameObject RootObj;
        public TextMesh NameLabel;
        public Vector3 TargetPosition;
        public float TargetRotY;
    }

    private readonly Dictionary<string, RemotePlayerAvatar> remotePlayers = new Dictionary<string, RemotePlayerAvatar>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private async void Start()
    {
        var player = FindFirstObjectByType<IsometricPlayerController>();
        if (player != null)
        {
            localPlayerTransform = player.transform;
        }

        await InitializeSoloSessionAsync();
    }

    public async Task InitializeSoloSessionAsync()
    {
        if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
        {
            LocalCharacterId = CharacterDataManager.Instance.CurrentCharacter.Id;
            LocalCharacterName = CharacterDataManager.Instance.CurrentCharacter.Name;
        }
        else
        {
            try
            {
                var client = SupabaseManager.Instance.Client;
                var resp = await client.From<CharacterModel>().Get();
                if (resp.Models.Count > 0)
                {
                    LocalCharacterId = resp.Models[0].Id;
                    LocalCharacterName = resp.Models[0].Name;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkSession] Erro ao obter personagem: {ex.Message}");
            }
        }

        if (string.IsNullOrEmpty(LocalCharacterId))
        {
            LocalCharacterId = Guid.NewGuid().ToString();
        }

        // Todo player começa em sua própria Sessão Solo por padrão
        CurrentSessionId = $"solo_{LocalCharacterId}";
        IsSoloSession = true;

        Debug.Log($"[NetworkSession] Sessão Solo iniciada para '{LocalCharacterName}' (Sessão: {CurrentSessionId})");

        await LoadFriendsAsync();
        OnSocialDataChanged?.Invoke();
    }

    private void Update()
    {
        // Interpola suavemente a posição dos amigos conectados na cena
        foreach (var kvp in remotePlayers)
        {
            if (kvp.Value.RootObj != null)
            {
                kvp.Value.RootObj.transform.position = Vector3.Lerp(
                    kvp.Value.RootObj.transform.position,
                    kvp.Value.TargetPosition,
                    Time.deltaTime * 12f
                );

                Quaternion targetRot = Quaternion.Euler(0f, kvp.Value.TargetRotY, 0f);
                kvp.Value.RootObj.transform.rotation = Quaternion.Slerp(
                    kvp.Value.RootObj.transform.rotation,
                    targetRot,
                    Time.deltaTime * 12f
                );
            }
        }

        // Sincroniza posição na sessão a cada 0.15s
        syncTimer += Time.deltaTime;
        if (syncTimer >= 0.15f && !isSyncing)
        {
            syncTimer = 0f;
            _ = SyncSessionStateAsync();
        }

        // Verifica convites recebidos a cada 2.0s
        invitePollTimer += Time.deltaTime;
        if (invitePollTimer >= 2.0f)
        {
            invitePollTimer = 0f;
            _ = PollIncomingInvitesAsync();
        }
    }

    public async Task UpdateCharacterNicknameAsync(string newNickname)
    {
        if (string.IsNullOrWhiteSpace(newNickname)) return;
        newNickname = newNickname.Trim();

        try
        {
            var client = SupabaseManager.Instance.Client;
            if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
            {
                var character = CharacterDataManager.Instance.CurrentCharacter;
                character.Name = newNickname;
                await client.From<CharacterModel>().Update(character);
            }

            LocalCharacterName = newNickname;
            Debug.Log($"[NetworkSession] Nickname atualizado para: {LocalCharacterName}");
            OnSocialDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkSession] Erro ao atualizar Nickname: {ex.Message}");
        }
    }

    public async Task LoadFriendsAsync()
    {
        if (string.IsNullOrEmpty(LocalCharacterId)) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            var response = await client.From<FriendshipModel>()
                .Where(x => x.CharacterId == LocalCharacterId)
                .Get();

            FriendsList = response.Models;
            OnSocialDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkSession] Erro ao carregar amigos: {ex.Message}");
        }
    }

    public async Task AddFriendAsync(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName)) return;
        friendName = friendName.Trim();

        if (FriendsList.Exists(f => f.FriendName.Equals(friendName, StringComparison.OrdinalIgnoreCase)))
        {
            Debug.Log($"[Social] '{friendName}' já está na sua lista de amigos.");
            return;
        }

        try
        {
            var client = SupabaseManager.Instance.Client;
            var newFriend = new FriendshipModel
            {
                CharacterId = LocalCharacterId,
                FriendName = friendName
            };

            var response = await client.From<FriendshipModel>().Insert(newFriend);
            if (response.Models.Count > 0)
            {
                FriendsList.Add(response.Models[0]);
                Debug.Log($"[Social] Amigo '{friendName}' adicionado com sucesso!");
                OnSocialDataChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Social] Erro ao adicionar amigo: {ex.Message}");
        }
    }

    public async Task SendSessionInviteAsync(string targetFriendName)
    {
        if (string.IsNullOrWhiteSpace(targetFriendName)) return;

        try
        {
            // Se estava em modo solo, batiza a sessão como sessão de grupo do Host
            if (IsSoloSession)
            {
                CurrentSessionId = $"party_{LocalCharacterId}";
                IsSoloSession = false;
            }

            var client = SupabaseManager.Instance.Client;
            var invite = new SessionInviteModel
            {
                SessionId = CurrentSessionId,
                SenderId = LocalCharacterId,
                SenderName = LocalCharacterName,
                ReceiverName = targetFriendName.Trim(),
                Status = "pending"
            };

            await client.From<SessionInviteModel>().Insert(invite);
            Debug.Log($"[Social] Convite de sessão enviado para '{targetFriendName}'!");
            OnSocialDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Social] Erro ao enviar convite: {ex.Message}");
        }
    }

    private async Task PollIncomingInvitesAsync()
    {
        if (string.IsNullOrEmpty(LocalCharacterName)) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            var response = await client.From<SessionInviteModel>()
                .Where(x => x.ReceiverName == LocalCharacterName && x.Status == "pending")
                .Get();

            if (response.Models.Count > 0)
            {
                PendingInvite = response.Models[0];
                OnSocialDataChanged?.Invoke();
            }
        }
        catch
        {
            // Ignora falhas silenciosas de polling
        }
    }

    public async void AcceptPendingInviteAsync()
    {
        if (PendingInvite == null) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            PendingInvite.Status = "accepted";
            await client.From<SessionInviteModel>().Update(PendingInvite);

            CurrentSessionId = PendingInvite.SessionId;
            IsSoloSession = false;
            Debug.Log($"[NetworkSession] Você entrou na sessão de '{PendingInvite.SenderName}'!");

            PendingInvite = null;
            ClearRemotePlayers();
            OnSocialDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Social] Erro ao aceitar convite: {ex.Message}");
        }
    }

    public async void DeclinePendingInviteAsync()
    {
        if (PendingInvite == null) return;

        try
        {
            var client = SupabaseManager.Instance.Client;
            PendingInvite.Status = "declined";
            await client.From<SessionInviteModel>().Update(PendingInvite);

            PendingInvite = null;
            OnSocialDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Social] Erro ao recusar convite: {ex.Message}");
        }
    }

    public void LeaveToSoloSession()
    {
        CurrentSessionId = $"solo_{LocalCharacterId}";
        IsSoloSession = true;
        ClearRemotePlayers();
        Debug.Log("[NetworkSession] Retornou para a Sessão Solo.");
        OnSocialDataChanged?.Invoke();
    }

    private async Task SyncSessionStateAsync()
    {
        if (localPlayerTransform == null || string.IsNullOrEmpty(LocalCharacterId)) return;
        isSyncing = true;

        try
        {
            var client = SupabaseManager.Instance.Client;

            // 1. Envia a posição atual do jogador local para o banco
            var myState = new SessionPlayerStateModel
            {
                CharacterId = LocalCharacterId,
                SessionId = CurrentSessionId,
                CharacterName = LocalCharacterName,
                PosX = localPlayerTransform.position.x,
                PosY = localPlayerTransform.position.y,
                PosZ = localPlayerTransform.position.z,
                RotY = localPlayerTransform.eulerAngles.y,
                UpdatedAt = DateTime.UtcNow
            };

            await client.From<SessionPlayerStateModel>().Upsert(myState);

            // 2. Busca outros jogadores que estejam no mesmo SessionId
            var response = await client.From<SessionPlayerStateModel>()
                .Where(x => x.SessionId == CurrentSessionId)
                .Get();

            HashSet<string> activeRemoteIds = new HashSet<string>();

            foreach (var state in response.Models)
            {
                if (state.CharacterId == LocalCharacterId) continue;

                activeRemoteIds.Add(state.CharacterId);
                UpdateOrSpawnRemotePlayer(state);
            }

            // Remove avatares de jogadores que saíram da sessão
            List<string> toRemove = new List<string>();
            foreach (var existingId in remotePlayers.Keys)
            {
                if (!activeRemoteIds.Contains(existingId))
                {
                    toRemove.Add(existingId);
                }
            }

            foreach (var remId in toRemove)
            {
                if (remotePlayers[remId].RootObj != null)
                {
                    Destroy(remotePlayers[remId].RootObj);
                }
                remotePlayers.Remove(remId);
            }
        }
        catch
        {
            // Evita poluir o console em oscilações de rede
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void UpdateOrSpawnRemotePlayer(SessionPlayerStateModel state)
    {
        Vector3 pos = new Vector3(state.PosX, state.PosY, state.PosZ);

        if (!remotePlayers.TryGetValue(state.CharacterId, out RemotePlayerAvatar avatar) || avatar.RootObj == null)
        {
            GameObject allyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            allyObj.name = $"RemotePlayer_{state.CharacterName}";
            Destroy(allyObj.GetComponent<Collider>());
            allyObj.transform.position = pos;

            if (allyObj.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = new Color(0.25f, 0.75f, 1f, 1f); // Azul celeste para destacar o aliado
            }

            // Nome flutuante sobre a cabeça do amigo
            GameObject textObj = new GameObject("NameTag");
            textObj.transform.SetParent(allyObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = state.CharacterName;
            tm.characterSize = 0.14f;
            tm.fontSize = 36;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.4f, 0.95f, 1f, 1f);

            avatar = new RemotePlayerAvatar
            {
                RootObj = allyObj,
                NameLabel = tm,
                TargetPosition = pos,
                TargetRotY = state.RotY
            };

            remotePlayers[state.CharacterId] = avatar;
            Debug.Log($"[NetworkSession] Jogador aliado '{state.CharacterName}' conectado na sua arena!");
        }
        else
        {
            avatar.TargetPosition = pos;
            avatar.TargetRotY = state.RotY;
            if (avatar.NameLabel != null)
            {
                avatar.NameLabel.text = state.CharacterName;
            }
        }
    }

    private void ClearRemotePlayers()
    {
        foreach (var kvp in remotePlayers)
        {
            if (kvp.Value.RootObj != null)
            {
                Destroy(kvp.Value.RootObj);
            }
        }
        remotePlayers.Clear();
    }
}