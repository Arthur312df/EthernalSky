using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Use UnityEngine.UI se estiver usando Text padrão do Unity

public class LoginUIController : MonoBehaviour
{
    [Header("Referências de UI (TextMeshPro)")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Configuração de Cena")]
    [SerializeField] private string gameSceneName = "Mundo"; // Nome da sua cena principal de combate

    public async void OnClickLoginButton()
    {
        string email = emailInputField != null ? emailInputField.text.Trim() : "";
        string password = passwordInputField != null ? passwordInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            UpdateStatus("Digite um e-mail e senha válidos!");
            return;
        }

        UpdateStatus("Conectando ao Supabase...");

        try
        {
            if (CharacterDataManager.Instance != null)
            {
                // Chama o método que criamos no CharacterDataManager para logar/cadastrar
                await CharacterDataManager.Instance.LoadSessionDataAsync(email, password);

                if (CharacterDataManager.Instance.CurrentCharacter != null)
                {
                    UpdateStatus("Login bem-sucedido! Entrando no jogo...");
                    
                    // Carrega a cena de jogo ou desativa o painel de login
                    SceneManager.LoadScene(gameSceneName);
                }
                else
                {
                    UpdateStatus("Erro: Não foi possível carregar o personagem.");
                }
            }
            else
            {
                UpdateStatus("Erro: CharacterDataManager não encontrado na cena!");
            }
        }
        catch (System.Exception ex)
        {
            UpdateStatus($"Erro de Autenticação: {ex.Message}");
            Debug.LogError($"[Login] Erro: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[Login UI] {message}");
    }
}