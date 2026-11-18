using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EthernalSky.Models;

public class InventoryUIController : MonoBehaviour
{
    public static bool IsInventoryOpen { get; private set; } = false;
    public static bool IsPointerOverInventory { get; private set; } = false;

    [Header("Configuração da Grade da Mochila")]
    [SerializeField] private int columns = 4;
    [SerializeField] private int rows = 5;

    private GameObject inventoryPanelObj;
    private RectTransform mainPanelRect;
    private Canvas inventoryCanvas;

    private readonly List<RectTransform> backpackSlotRects = new List<RectTransform>();
    private readonly List<Image> backpackSlotBgs = new List<Image>();
    private readonly List<Text> backpackNameTexts = new List<Text>();
    private readonly List<Text> backpackQtyTexts = new List<Text>();

    // Array fixo de 20 posições para permitir mover itens para qualquer slot vazio da mochila
    private InventoryItemModel[] backpackGridSlots;

    private class EquipSlotUI
    {
        public EquipmentSlotType SlotType;
        public string DefaultLabel;
        public RectTransform Rect;
        public Image Background;
        public Text NameText;
        public Text SubText;
    }

    private readonly List<EquipSlotUI> equipmentSlots = new List<EquipSlotUI>();
    private Text headerStatusText;
    private Text bonusStatsText;
    private Text goldCounterText;
    private Font uiFont;

    // Estado de Drag & Drop (Segurar e Arrastar)
    private InventoryItemModel draggedItem = null;
    private bool draggedFromEquipment = false;
    private int draggedOriginIndex = -1;
    private Vector2 pointerDownPos;
    private bool isDragging = false;

    private RectTransform dragGhostRect;
    private Text dragGhostText;

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        backpackGridSlots = new InventoryItemModel[columns * rows];

        BuildInventoryUI();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        }

        SetInventoryOpen(false);
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
        }
        IsInventoryOpen = false;
        IsPointerOverInventory = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            SetInventoryOpen(!IsInventoryOpen);
        }

        if (!IsInventoryOpen || mainPanelRect == null || Mouse.current == null)
        {
            IsPointerOverInventory = false;
            CancelDrag();
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool insideWindow = RectTransformUtility.RectangleContainsScreenPoint(mainPanelRect, mousePos, null);

        // Enquanto estiver segurando um item, bloqueia ataques do personagem mesmo fora da janela
        IsPointerOverInventory = insideWindow || draggedItem != null;

        // 1. Pressionou o botão esquerdo em cima de um slot
        if (Mouse.current.leftButton.wasPressedThisFrame && insideWindow)
        {
            TryBeginHoldItem(mousePos);
        }

        // 2. Está segurando o botão esquerdo e movendo o mouse
        if (draggedItem != null && Mouse.current.leftButton.isPressed)
        {
            if (!isDragging && Vector2.Distance(mousePos, pointerDownPos) > 8f)
            {
                isDragging = true;
                if (dragGhostRect != null)
                {
                    ItemMetadata meta = InventoryManager.Instance.GetItemMetadata(draggedItem.ItemKey);
                    dragGhostText.text = meta.DisplayName;
                    dragGhostRect.gameObject.SetActive(true);
                }
            }

            if (isDragging && dragGhostRect != null)
            {
                dragGhostRect.position = mousePos;
            }
        }

        // 3. Soltou o botão esquerdo do mouse
        if (draggedItem != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CompletePointerRelease(mousePos, insideWindow);
        }
    }

    private void TryBeginHoldItem(Vector2 mousePos)
    {
        if (InventoryManager.Instance == null) return;

        // Verifica se clicou em um slot da Mochila
        for (int i = 0; i < backpackSlotRects.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(backpackSlotRects[i], mousePos, null))
            {
                if (backpackGridSlots[i] != null)
                {
                    draggedItem = backpackGridSlots[i];
                    draggedFromEquipment = false;
                    draggedOriginIndex = i;
                    pointerDownPos = mousePos;
                    isDragging = false;
                }
                return;
            }
        }

        // Verifica se clicou em um slot do Corpo (Equipamento)
        foreach (var eqSlot in equipmentSlots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(eqSlot.Rect, mousePos, null))
            {
                InventoryItemModel equipped = InventoryManager.Instance.GetEquippedItemInSlot(eqSlot.SlotType);
                if (equipped != null)
                {
                    draggedItem = equipped;
                    draggedFromEquipment = true;
                    draggedOriginIndex = -1;
                    pointerDownPos = mousePos;
                    isDragging = false;
                }
                return;
            }
        }
    }

    private void CompletePointerRelease(Vector2 mousePos, bool insideWindow)
    {
        if (InventoryManager.Instance == null || draggedItem == null)
        {
            CancelDrag();
            return;
        }

        // Se foi apenas um clique rápido sem arrastar: equipa ou desequipa automaticamente
        if (!isDragging)
        {
            if (draggedFromEquipment)
            {
                InventoryManager.Instance.UnequipItemAsync(draggedItem);
            }
            else
            {
                InventoryManager.Instance.EquipItemAsync(draggedItem);
            }
            CancelDrag();
            return;
        }

        // CASO 1: Arrastou para FORA da janela do Inventário -> Joga o item no chão!
        if (!insideWindow)
        {
            InventoryManager.Instance.DropItemToWorldAsync(draggedItem);
            CancelDrag();
            return;
        }

        // CASO 2: Soltou em cima de um slot de Equipamento no Corpo
        foreach (var eqSlot in equipmentSlots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(eqSlot.Rect, mousePos, null))
            {
                ItemMetadata meta = InventoryManager.Instance.GetItemMetadata(draggedItem.ItemKey);
                if (meta.SlotType == eqSlot.SlotType && !draggedItem.IsEquipped)
                {
                    InventoryManager.Instance.EquipItemAsync(draggedItem);
                }
                CancelDrag();
                return;
            }
        }

        // CASO 3: Soltou em cima de um slot da Mochila (mover de lugar ou desequipar do corpo)
        for (int i = 0; i < backpackSlotRects.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(backpackSlotRects[i], mousePos, null))
            {
                if (draggedFromEquipment)
                {
                    InventoryManager.Instance.UnequipItemAsync(draggedItem);
                }
                else if (draggedOriginIndex >= 0 && draggedOriginIndex != i)
                {
                    // Troca os itens de posição diretamente na grade da mochila
                    InventoryItemModel targetItem = backpackGridSlots[i];
                    backpackGridSlots[i] = draggedItem;
                    backpackGridSlots[draggedOriginIndex] = targetItem;
                    UpdateVisualSlotsFromGrid();
                }
                CancelDrag();
                return;
            }
        }

        CancelDrag();
    }

    private void CancelDrag()
    {
        draggedItem = null;
        isDragging = false;
        draggedOriginIndex = -1;
        if (dragGhostRect != null)
        {
            dragGhostRect.gameObject.SetActive(false);
        }
    }

    public void SetInventoryOpen(bool open)
    {
        IsInventoryOpen = open;
        if (!open)
        {
            IsPointerOverInventory = false;
            CancelDrag();
        }

        if (inventoryPanelObj != null)
        {
            inventoryPanelObj.SetActive(open);
        }

        if (open)
        {
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        if (backpackNameTexts.Count == 0) return;

        int totalSlots = columns * rows;
        if (backpackGridSlots == null || backpackGridSlots.Length != totalSlots)
        {
            backpackGridSlots = new InventoryItemModel[totalSlots];
        }

        // Mantém itens nas posições escolhidas pelo jogador e aloca itens novos no primeiro slot livre
        List<InventoryItemModel> unequippedItems = new List<InventoryItemModel>();
        if (InventoryManager.Instance != null)
        {
            foreach (var item in InventoryManager.Instance.PlayerInventory)
            {
                if (item != null && !item.IsEquipped && item.ItemKey != "gold_coin")
                {
                    unequippedItems.Add(item);
                }
            }
        }

        // Remove da grade itens que foram equipados ou jogados no chão
        HashSet<string> placedIds = new HashSet<string>();
        for (int i = 0; i < totalSlots; i++)
        {
            if (backpackGridSlots[i] != null)
            {
                var match = unequippedItems.Find(x => x.Id == backpackGridSlots[i].Id);
                if (match != null)
                {
                    backpackGridSlots[i] = match;
                    placedIds.Add(match.Id);
                }
                else
                {
                    backpackGridSlots[i] = null;
                }
            }
        }

        // Coloca itens recém-coletados nos primeiros slots vazios
        foreach (var item in unequippedItems)
        {
            if (!placedIds.Contains(item.Id))
            {
                for (int i = 0; i < totalSlots; i++)
                {
                    if (backpackGridSlots[i] == null)
                    {
                        backpackGridSlots[i] = item;
                        placedIds.Add(item.Id);
                        break;
                    }
                }
            }
        }

        UpdateVisualSlotsFromGrid();
    }

    private void UpdateVisualSlotsFromGrid()
    {
        // Atualiza os 6 slots do Corpo
        foreach (var eqSlot in equipmentSlots)
        {
            InventoryItemModel equippedItem = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetEquippedItemInSlot(eqSlot.SlotType)
                : null;

            if (equippedItem != null)
            {
                ItemMetadata meta = InventoryManager.Instance.GetItemMetadata(equippedItem.ItemKey);
                eqSlot.NameText.text = meta.DisplayName;
                eqSlot.NameText.color = new Color(1f, 0.9f, 0.4f, 1f);
                eqSlot.SubText.text = "EQUIPADO";
                eqSlot.SubText.color = new Color(0.4f, 0.95f, 0.5f, 1f);
                eqSlot.Background.color = new Color(0.24f, 0.28f, 0.38f, 1f);
            }
            else
            {
                eqSlot.NameText.text = eqSlot.DefaultLabel;
                eqSlot.NameText.color = new Color(0.5f, 0.52f, 0.58f, 1f);
                eqSlot.SubText.text = "Vazio";
                eqSlot.SubText.color = new Color(0.35f, 0.36f, 0.4f, 1f);
                eqSlot.Background.color = new Color(0.13f, 0.14f, 0.18f, 1f);
            }
        }

        // Atualiza os 20 slots da Mochila
        int totalSlots = columns * rows;
        int occupiedCount = 0;

        for (int i = 0; i < totalSlots; i++)
        {
            InventoryItemModel item = backpackGridSlots[i];
            if (item != null)
            {
                occupiedCount++;
                ItemMetadata meta = InventoryManager.Instance.GetItemMetadata(item.ItemKey);

                backpackNameTexts[i].text = meta.DisplayName;
                backpackNameTexts[i].color = Color.white;
                backpackQtyTexts[i].text = meta.IsStackable ? $"x{item.Quantity}" : "";
                backpackSlotBgs[i].color = new Color(0.22f, 0.24f, 0.32f, 1f);
            }
            else
            {
                backpackNameTexts[i].text = "Vazio";
                backpackNameTexts[i].color = new Color(0.45f, 0.45f, 0.5f, 1f);
                backpackQtyTexts[i].text = "";
                backpackSlotBgs[i].color = new Color(0.14f, 0.14f, 0.18f, 1f);
            }
        }

        if (headerStatusText != null)
        {
            headerStatusText.text = $"Slots: {occupiedCount} / {totalSlots}  (Arraste para fora para jogar no chão)";
        }

        if (InventoryManager.Instance != null)
        {
            if (bonusStatsText != null)
            {
                bonusStatsText.text = $"Bônus Ativo: +{InventoryManager.Instance.TotalBonusDamage:F0} Dano  |  +{InventoryManager.Instance.TotalBonusHp:F0} Vida";
            }

            if (goldCounterText != null)
            {
                goldCounterText.text = $"Ouro: {InventoryManager.Instance.TotalGold}";
            }
        }
    }

    private void BuildInventoryUI()
    {
        GameObject canvasObj = new GameObject("InventoryHUD_Canvas");
        inventoryCanvas = canvasObj.AddComponent<Canvas>();
        inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inventoryCanvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        mainPanelRect = CreatePanel(
            "Inventory_MainPanel",
            canvasObj.transform,
            new Vector2(760f, 605f),
            new Vector2(-40f, 40f),
            new Color(0.08f, 0.08f, 0.1f, 0.95f),
            out _
        );
        mainPanelRect.anchorMin = new Vector2(1f, 0.5f);
        mainPanelRect.anchorMax = new Vector2(1f, 0.5f);
        mainPanelRect.pivot = new Vector2(1f, 0.5f);
        inventoryPanelObj = mainPanelRect.gameObject;

        // LADO ESQUERDO: CORPO E EQUIPAMENTOS
        RectTransform equipArea = CreatePanel(
            "Equipment_Area",
            mainPanelRect,
            new Vector2(320f, 556f),
            new Vector2(-190f, 0f),
            new Color(0.06f, 0.06f, 0.08f, 0.9f),
            out _
        );

        Text equipTitle = CreateText("EquipTitle", equipArea, "EQUIPAMENTOS", 15, TextAnchor.UpperCenter, new Color(0.95f, 0.8f, 0.3f, 1f));
        equipTitle.rectTransform.offsetMax = new Vector2(-10f, -14f);

        Color silhouetteColor = new Color(0.11f, 0.12f, 0.15f, 0.9f);
        CreatePanel("Body_Head", equipArea, new Vector2(46f, 46f), new Vector2(0f, 145f), silhouetteColor, out _);
        CreatePanel("Body_Torso", equipArea, new Vector2(84f, 120f), new Vector2(0f, 45f), silhouetteColor, out _);
        CreatePanel("Body_LeftArm", equipArea, new Vector2(32f, 105f), new Vector2(-66f, 48f), silhouetteColor, out _);
        CreatePanel("Body_RightArm", equipArea, new Vector2(32f, 105f), new Vector2(66f, 48f), silhouetteColor, out _);
        CreatePanel("Body_Legs", equipArea, new Vector2(72f, 130f), new Vector2(0f, -92f), silhouetteColor, out _);

        CreateEquipmentSlot(equipArea, EquipmentSlotType.Helmet, "Capacete", new Vector2(0f, 150f));
        CreateEquipmentSlot(equipArea, EquipmentSlotType.Chest, "Peitoral", new Vector2(0f, 48f));
        CreateEquipmentSlot(equipArea, EquipmentSlotType.Weapon, "Arma", new Vector2(-104f, 48f));
        CreateEquipmentSlot(equipArea, EquipmentSlotType.Gloves, "Luvas", new Vector2(104f, 48f));
        CreateEquipmentSlot(equipArea, EquipmentSlotType.Pants, "Calça", new Vector2(0f, -54f));
        CreateEquipmentSlot(equipArea, EquipmentSlotType.Boots, "Botas", new Vector2(0f, -156f));

        bonusStatsText = CreateText("BonusStats", equipArea, "Bônus Ativo: +0 Dano  |  +0 Vida", 12, TextAnchor.LowerCenter, new Color(0.55f, 0.85f, 1f, 1f));
        bonusStatsText.rectTransform.offsetMin = new Vector2(10f, 14f);

        // LADO DIREITO: GRADE DA MOCHILA (4x5)
        RectTransform backpackArea = CreatePanel(
            "Backpack_Area",
            mainPanelRect,
            new Vector2(390f, 556f),
            new Vector2(175f, 0f),
            new Color(0f, 0f, 0f, 0f),
            out _
        );

        Text titleText = CreateText("Title", backpackArea, "MOCHILA  [TAB]", 16, TextAnchor.UpperCenter, new Color(0.95f, 0.8f, 0.3f, 1f));
        titleText.rectTransform.offsetMax = new Vector2(-10f, -12f);

        headerStatusText = CreateText("Subtitle", backpackArea, "Slots: 0 / 20", 11, TextAnchor.UpperCenter, new Color(0.7f, 0.75f, 0.85f, 1f));
        headerStatusText.rectTransform.offsetMax = new Vector2(-10f, -36f);

        float slotSize = 84f;
        float spacing = 12f;
        float startX = -((columns - 1) * (slotSize + spacing)) * 0.5f;
        float startY = 172f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector2 pos = new Vector2(startX + c * (slotSize + spacing), startY - r * (slotSize + spacing));
                RectTransform slotRect = CreatePanel(
                    $"InvSlot_{r}_{c}",
                    backpackArea,
                    new Vector2(slotSize, slotSize),
                    pos,
                    new Color(0.14f, 0.14f, 0.18f, 1f),
                    out Image slotBg
                );

                Text itemName = CreateText("ItemName", slotRect, "Vazio", 12, TextAnchor.MiddleCenter, new Color(0.45f, 0.45f, 0.5f, 1f));
                Text itemQty = CreateText("ItemQty", slotRect, "", 13, TextAnchor.LowerRight, Color.yellow);
                itemQty.rectTransform.offsetMin = new Vector2(4f, 4f);
                itemQty.rectTransform.offsetMax = new Vector2(-6f, -4f);

                backpackSlotRects.Add(slotRect);
                backpackSlotBgs.Add(slotBg);
                backpackNameTexts.Add(itemName);
                backpackQtyTexts.Add(itemQty);
            }
        }

        RectTransform goldBar = CreatePanel(
            "Gold_Bar",
            backpackArea,
            new Vector2(372f, 28f),
            new Vector2(0f, -258f),
            new Color(0.06f, 0.06f, 0.08f, 0.9f),
            out _
        );
        goldCounterText = CreateText("GoldText", goldBar, "Ouro: 0", 13, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.25f, 1f));
        goldCounterText.rectTransform.offsetMax = new Vector2(-14f, 0f);

        // Ícone Flutuante de Arrasto (Drag Ghost)
        dragGhostRect = CreatePanel(
            "DragGhost_Icon",
            canvasObj.transform,
            new Vector2(84f, 84f),
            Vector2.zero,
            new Color(0.3f, 0.36f, 0.5f, 0.85f),
            out Image ghostImg
        );
        ghostImg.raycastTarget = false;
        dragGhostText = CreateText("GhostText", dragGhostRect, "", 12, TextAnchor.MiddleCenter, Color.yellow);
        dragGhostText.raycastTarget = false;
        dragGhostRect.gameObject.SetActive(false);
    }

    private void CreateEquipmentSlot(Transform parent, EquipmentSlotType slotType, string label, Vector2 anchoredPos)
    {
        RectTransform slotRect = CreatePanel(
            $"EquipSlot_{slotType}",
            parent,
            new Vector2(86f, 86f),
            anchoredPos,
            new Color(0.13f, 0.14f, 0.18f, 1f),
            out Image slotBg
        );

        Text nameTxt = CreateText("EquipName", slotRect, label, 12, TextAnchor.MiddleCenter, new Color(0.5f, 0.52f, 0.58f, 1f));
        Text subTxt = CreateText("EquipStatus", slotRect, "Vazio", 10, TextAnchor.LowerCenter, new Color(0.35f, 0.36f, 0.4f, 1f));
        subTxt.rectTransform.offsetMin = new Vector2(4f, 5f);

        equipmentSlots.Add(new EquipSlotUI
        {
            SlotType = slotType,
            DefaultLabel = label,
            Rect = slotRect,
            Background = slotBg,
            NameText = nameTxt,
            SubText = subTxt
        });
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
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);
        return txt;
    }
}