using System;
using System.Threading.Tasks;
using UnityEngine;

public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance { get; private set; }

    [Header("Supabase Credentials")]
    [SerializeField] private string supabaseUrl = "SUA_SUPABASE_URL";
    [SerializeField] private string supabaseKey = "SUA_SUPABASE_ANON_KEY";

    public Supabase.Client Client { get; private set; }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeSupabaseAsync();
    }

    private async Task InitializeSupabaseAsync()
    {
        try
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            Client = new Supabase.Client(supabaseUrl, supabaseKey, options);
            await Client.InitializeAsync();

            Debug.Log("[SupabaseManager] Conectado com sucesso ao banco de dados do EthernalSky!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SupabaseManager] Falha ao inicializar o Supabase: {ex.Message}");
        }
    }
}