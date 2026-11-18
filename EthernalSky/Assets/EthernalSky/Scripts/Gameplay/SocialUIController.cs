using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EthernalSky.Models;

public class SocialUIController : MonoBehaviour
{
    public static bool IsSocialOpen { get; private set; } = false;
    public static bool IsPointerOverSocial { get; private set; } = false;

    private GameObject socialPanelObj;
    private RectTransform mainPanelRect;
    private GameObject invitePopupObj;
    private Text invitePopupText;

    private Text sessionStatusText;
    private InputField nicknameInput;
    private InputField addFriendInput;
    private Transform friendsListContainer;
    private GameObject leaveSessionBtnObj;

    private readonly List<GameObject> spawnedFriendRows = new List<GameObject>();
    private Font uiFont;

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildSocialUI();

        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnSocialDataChanged += RefreshUI;
        }

        SetSocialOpen(false);
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnSocialDataChanged -= RefreshUI;
        }
        IsSocialOpen = false;
        IsPointerOverSocial = false;
    }

    private void Update()
    {
        // Evita abrir/fechar a janela se o jogador estiver digitando em um campo de texto
        bool isTyping = (nicknameInput != null && nicknameInput.isFocused) ||
                        (addFriendInput != null && addFriendInput.isFocused);

        if (!isTyping && Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
            SetSocialOpen(!IsSocialOpen);
        }

        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        bool overMain = IsSocialOpen && mainPanelRect != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(mainPanelRect, mousePos, null);

        bool overPopup = invitePopupObj != null && invitePopupObj.activeSelf &&
                         RectTransformUtility.RectangleContainsScreenPoint(invitePopupObj.GetComponent<RectTransform>(), mousePos, null);

        IsPointerOverSocial = overMain || overPopup;
    }

    public void SetSocialOpen(bool open)
    {
        IsSocialOpen = open;
        if (socialPanelObj != null)
        {
            socialPanelObj.SetActive(open);
        }

        if (open)
        {
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        var net = NetworkSessionManager.Instance;
        if (net == null) return;

        if (sessionStatusText != null)
        {
            sessionStatusText.text = net.IsSoloSession
                ? "Modo Atual: SESSÃO SOLO"
                : $"Modo Atual: EM GRUPO ({net.CurrentSessionId.Substring(0, Mathf.Min(12, net.CurrentSessionId.Length))}...)";

            sessionStatusText.color = net.IsSoloSession
                ? new Color(0.65f, 0.8f, 0.95f, 1f)
                : new Color(0.4f, 0.95f, 0.5f, 1f);
        }

        if (nicknameInput != null && !nicknameInput.isFocused)
        {
            nicknameInput.text = net.LocalCharacterName;
        }

        if (leaveSessionBtnObj != null)
        {
            leaveSessionBtnObj.SetActive(!net.IsSoloSession);
        }

        // Reconstrói a lista visual de amigos
        foreach (var row in spawnedFriendRows)
        {
            Destroy(row);
        }
        spawnedFriendRows.Clear();

        if (friendsListContainer != null)
        {
            int maxVisible = Mathf.Min(net.FriendsList.Count, 6);
            for (int i = 0; i < maxVisible; i++)
            {
                FriendshipModel friend = net.FriendsList[i];
                CreateFriendRow(friend.FriendName, i);
            }
        }

        // Exibe ou oculta o Popup de Convite Recebido
        if (invitePopupObj != null)
        {
            if (net.PendingInvite != null)
            {
                invitePopupObj.SetActive(true);
                invitePopupText.text = $"<b>{net.PendingInvite.SenderName}</b> convidou você para jogar na sessão dele!";
            }
            else
            {
                invitePopupObj.SetActive(false);
            }
        }
    }

    private void CreateFriendRow(string friendName, int index)
    {
        float startY = 85f;
        float rowHeight = 38f;
        float spacing = 6f;

        RectTransform rowRect = CreatePanel(
            $"FriendRow_{index}",
            friendsListContainer,
            new Vector2(320f, rowHeight),
            new Vector2(0f, startY - index * (rowHeight + spacing)),
            new Color(0.14f, 0.15f, 0.2f, 1f),
            out _
        );

        Text nameTxt = CreateText("FriendName", rowRect, friendName, 13, TextAnchor.MiddleLeft, Color.white);
        nameTxt.rectTransform.offsetMin = new Vector2(12f, 4f);
        nameTxt.rectTransform.offsetMax = new Vector2(-110f, -4f);

        CreateButton(
            "InviteBtn",
            rowRect,
            "Convidar",
            new Vector2(86f, 28f),
            new Vector2(108f, 0f),
            new Color(0.2f, 0.55f, 0.3f, 1f),
            () => _ = NetworkSessionManager.Instance.SendSessionInviteAsync(friendName)
        );

        spawnedFriendRows.Add(rowRect.gameObject);
    }

    private void BuildSocialUI()
    {
        GameObject canvasObj = new GameObject("SocialHUD_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Painel Social posicionado no lado esquerdo da tela (Tecla O)
        mainPanelRect = CreatePanel(
            "Social_MainPanel",
            canvasObj.transform,
            new Vector2(360f, 520f),
            new Vector2(40f, 40f),
            new Color(0.08f, 0.08f, 0.1f, 0.95f),
            out _
        );
        mainPanelRect.anchorMin = new Vector2(0f, 0.5f);
        mainPanelRect.anchorMax = new Vector2(0f, 0.5f);
        mainPanelRect.pivot = new Vector2(0f, 0.5f);
        socialPanelObj = mainPanelRect.gameObject;

        Text title = CreateText("Title", mainPanelRect, "SOCIAL E SESSÃO  [O]", 16, TextAnchor.UpperCenter, new Color(0.95f, 0.8f, 0.3f, 1f));
        title.rectTransform.offsetMax = new Vector2(-10f, -14f);

        sessionStatusText = CreateText("SessionStatus", mainPanelRect, "Modo Atual: SESSÃO SOLO", 12, TextAnchor.UpperCenter, new Color(0.65f, 0.8f, 0.95f, 1f));
        sessionStatusText.rectTransform.offsetMax = new Vector2(-10f, -38f);

        // Campo para definir o próprio Nickname
        Text nickLabel = CreateText("NickLabel", mainPanelRect, "Seu Nickname (para amigos te convidarem):", 11, TextAnchor.UpperLeft, new Color(0.75f, 0.75f, 0.8f, 1f));
        nickLabel.rectTransform.offsetMin = new Vector2(20f, 0f);
        nickLabel.rectTransform.offsetMax = new Vector2(-20f, -68f);

        nicknameInput = CreateInputField("NicknameInput", mainPanelRect, new Vector2(215f, 32f), new Vector2(-52f, 158f), "Seu Nickname...");
        CreateButton(
            "SaveNickBtn",
            mainPanelRect,
            "Salvar",
            new Vector2(95f, 32f),
            new Vector2(112f, 158f),
            new Color(0.22f, 0.45f, 0.75f, 1f),
            () => _ = NetworkSessionManager.Instance.UpdateCharacterNicknameAsync(nicknameInput.text)
        );

        // Campo para adicionar amigo pelo Nickname
        Text addLabel = CreateText("AddLabel", mainPanelRect, "Adicionar Amigo pelo Nickname:", 11, TextAnchor.UpperLeft, new Color(0.75f, 0.75f, 0.8f, 1f));
        addLabel.rectTransform.offsetMin = new Vector2(20f, 0f);
        addLabel.rectTransform.offsetMax = new Vector2(-20f, -135f);

        addFriendInput = CreateInputField("AddFriendInput", mainPanelRect, new Vector2(215f, 32f), new Vector2(-52f, 92f), "Nome do amigo...");
        CreateButton(
            "AddFriendBtn",
            mainPanelRect,
            "Adicionar",
            new Vector2(95f, 32f),
            new Vector2(112f, 92f),
            new Color(0.25f, 0.5f, 0.35f, 1f),
            () =>
            {
                _ = NetworkSessionManager.Instance.AddFriendAsync(addFriendInput.text);
                addFriendInput.text = "";
            }
        );

        // Container da Lista de Amigos
        RectTransform listBg = CreatePanel(
            "FriendsList_Bg",
            mainPanelRect,
            new Vector2(330f, 270f),
            new Vector2(0f, -75f),
            new Color(0.05f, 0.05f, 0.07f, 0.9f),
            out _
        );
        friendsListContainer = listBg.transform;

        // Botão para voltar à Sessão Solo quando estiver em grupo
        Button leaveBtn = CreateButton(
            "LeaveSessionBtn",
            mainPanelRect,
            "Sair do Grupo (Voltar p/ Solo)",
            new Vector2(320f, 32f),
            new Vector2(0f, -234f),
            new Color(0.65f, 0.22f, 0.22f, 1f),
            () => NetworkSessionManager.Instance.LeaveToSoloSession()
        );
        leaveSessionBtnObj = leaveBtn.gameObject;

        // =========================================================
        // POPUP DE CONVITE RECEBIDO (Fica no topo da tela)
        // =========================================================
        RectTransform popupRect = CreatePanel(
            "InvitePopup_Panel",
            canvasObj.transform,
            new Vector2(420f, 115f),
            new Vector2(0f, -30f),
            new Color(0.1f, 0.12f, 0.18f, 0.96f),
            out _
        );
        popupRect.anchorMin = new Vector2(0.5f, 1f);
        popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 1f);
        invitePopupObj = popupRect.gameObject;

        invitePopupText = CreateText("InviteText", popupRect, "Convite de Sessão recebido!", 14, TextAnchor.UpperCenter, Color.white);
        invitePopupText.rectTransform.offsetMax = new Vector2(-12f, -16f);

        CreateButton(
            "AcceptBtn",
            popupRect,
            "Aceitar e Entrar",
            new Vector2(150f, 34f),
            new Vector2(-85f, -26f),
            new Color(0.2f, 0.65f, 0.32f, 1f),
            () => NetworkSessionManager.Instance.AcceptPendingInviteAsync()
        );

        CreateButton(
            "DeclineBtn",
            popupRect,
            "Recusar",
            new Vector2(150f, 34f),
            new Vector2(85f, -26f),
            new Color(0.65f, 0.22f, 0.22f, 1f),
            () => NetworkSessionManager.Instance.DeclinePendingInviteAsync()
        );

        invitePopupObj.SetActive(false);
    }

    private InputField CreateInputField(string name, Transform parent, Vector2 size, Vector2 anchoredPos, string placeholderText)
    {
        RectTransform bg = CreatePanel(name, parent, size, anchoredPos, new Color(0.14f, 0.15f, 0.2f, 1f), out Image img);
        InputField input = bg.gameObject.AddComponent<InputField>();

        Text textComp = CreateText("Text", bg, "", 13, TextAnchor.MiddleLeft, Color.white);
        textComp.supportRichText = false;

        Text placeholder = CreateText("Placeholder", bg, placeholderText, 12, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.55f, 1f));

        input.targetGraphic = img;
        input.textComponent = textComp;
        input.placeholder = placeholder;
        return input;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 anchoredPos, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreatePanel(name, parent, size, anchoredPos, color, out Image img);
        Button btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        CreateText("BtnLabel", rect, label, 12, TextAnchor.MiddleCenter, Color.white);
        return btn;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchoredPos, Color color, out Image imageComponent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        imageComponent = obj.AddComponent<Image>();
        imageComponent.color = color;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;
        return rect;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text txt = obj.AddComponent<Text>();
        txt.font = uiFont;
        txt.text = content;
        txt.fontSize = fontSize;
        txt.alignment = alignment;
        txt.color = color;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 4f);
        rect.offsetMax = new Vector2(-6f, -4f);
        return txt;
    }
}