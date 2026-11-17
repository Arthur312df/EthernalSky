using UnityEngine;
using UnityEngine.UI;

public class CombatHUDManager : MonoBehaviour
{
    [Header("Referência do Jogador")]
    [SerializeField] private PlayerCombatController playerCombat;

    private RectTransform hpFillRect;
    private Text hpText;
    private RectTransform manaFillRect;
    private Text manaText;

    private RectTransform[] cooldownOverlayRects = new RectTransform[6];
    private Text[] cooldownTexts = new Text[6];
    private Text[] skillNameTexts = new Text[6];

    private readonly string[] keybindLabels = { "LMB", "RMB", "Q", "E", "R", "SPACE" };
    private Font uiFont;

    private void Start()
    {
        if (playerCombat == null)
        {
            playerCombat = FindFirstObjectByType<PlayerCombatController>();
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildHUD();
    }

    private void Update()
    {
        if (playerCombat == null) return;

        UpdateVitals();
        UpdateSkillSlots();
    }

    private void UpdateVitals()
    {
        float hpRatio = playerCombat.MaxHp > 0f ? Mathf.Clamp01(playerCombat.CurrentHp / playerCombat.MaxHp) : 0f;
        hpFillRect.anchorMax = new Vector2(hpRatio, 1f);
        hpText.text = $"HP: {playerCombat.CurrentHp:F0} / {playerCombat.MaxHp:F0}";

        float manaRatio = playerCombat.MaxMana > 0f ? Mathf.Clamp01(playerCombat.CurrentMana / playerCombat.MaxMana) : 0f;
        manaFillRect.anchorMax = new Vector2(manaRatio, 1f);
        manaText.text = $"MANA: {playerCombat.CurrentMana:F0} / {playerCombat.MaxMana:F0}";
    }

    private void UpdateSkillSlots()
    {
        for (int i = 0; i < 6; i++)
        {
            SkillData skill = playerCombat.GetSkillInSlot(i);
            float remaining = playerCombat.GetCooldownRemaining(i);
            float ratio = playerCombat.GetCooldownRatio(i);

            skillNameTexts[i].text = skill != null ? skill.displayName : "Vazio";
            cooldownOverlayRects[i].anchorMax = new Vector2(1f, ratio);
            cooldownTexts[i].text = remaining > 0.05f ? $"{remaining:F1}s" : "";
        }
    }

    private void BuildHUD()
    {
        GameObject canvasObj = new GameObject("CombatHUD_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Painel principal compacto e alinhado na base do ecrã
        RectTransform mainPanel = CreatePanel("HUD_MainPanel", canvasObj.transform, new Vector2(530f, 130f), new Vector2(0f, 20f), new Color(0.08f, 0.08f, 0.1f, 0.9f));
        mainPanel.anchorMin = new Vector2(0.5f, 0f);
        mainPanel.anchorMax = new Vector2(0.5f, 0f);
        mainPanel.pivot = new Vector2(0.5f, 0f);

        // Coordenadas relativas ao centro do mainPanel (Y varia de -65 a +65)
        CreateBar(mainPanel, "HP_Bar", new Vector2(-130f, 38f), new Vector2(240f, 22f), new Color(0.75f, 0.18f, 0.18f, 1f), out hpFillRect, out hpText);
        CreateBar(mainPanel, "Mana_Bar", new Vector2(130f, 38f), new Vector2(240f, 22f), new Color(0.18f, 0.45f, 0.85f, 1f), out manaFillRect, out manaText);

        float startX = -212.5f;
        float spacing = 85f;

        for (int i = 0; i < 6; i++)
        {
            RectTransform slotRect = CreatePanel($"Slot_{i}", mainPanel, new Vector2(75f, 75f), new Vector2(startX + (i * spacing), -16f), new Color(0.16f, 0.16f, 0.2f, 1f));

            skillNameTexts[i] = CreateText($"SkillName_{i}", slotRect, "Vazio", 11, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f, 1f));

            RectTransform overlay = CreatePanel($"CooldownOverlay_{i}", slotRect, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = new Vector2(1f, 0f);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            cooldownOverlayRects[i] = overlay;

            cooldownTexts[i] = CreateText($"CooldownText_{i}", slotRect, "", 16, TextAnchor.MiddleCenter, Color.yellow);

            Text keyText = CreateText($"Keybind_{i}", slotRect, keybindLabels[i], 11, TextAnchor.LowerCenter, new Color(0.6f, 0.8f, 1f, 1f));
            keyText.rectTransform.offsetMin = new Vector2(0f, 4f);
        }
    }

    private void CreateBar(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color fillColor, out RectTransform fillRect, out Text labelText)
    {
        RectTransform bg = CreatePanel(name, parent, size, anchoredPos, new Color(0.04f, 0.04f, 0.05f, 1f));

        fillRect = CreatePanel($"{name}_Fill", bg, Vector2.zero, Vector2.zero, fillColor);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        labelText = CreateText($"{name}_Text", bg, "", 12, TextAnchor.MiddleCenter, Color.white);
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchoredPos, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = color;

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
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);
        return txt;
    }
}