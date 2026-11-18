using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCombatController : MonoBehaviour
{
    [Header("Atributos de Vida e Mana")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float currentHp = 100f;
    [SerializeField] private float hpRegenRate = 2f;

    [SerializeField] private float maxMana = 50f;
    [SerializeField] private float currentMana = 50f;
    [SerializeField] private float manaRegenRate = 5f;

    [Header("Configuração de Combate (1 Ataque Básico + 5 Skills)")]
    [SerializeField] private SkillData basicAttackSkill;
    [SerializeField] private List<SkillData> equippedSkills = new List<SkillData>(6);
    [SerializeField] private LayerMask groundLayerMask = ~0;

    private NetworkObject networkObject;
    private CharacterController characterController;
    private IsometricPlayerController movementController;
    private Camera mainCamera;

    private readonly float[] skillCooldowns = new float[6];
    private readonly float[] skillMaxCooldowns = new float[6];
    private bool isDashing = false;

    public float BonusHp => InventoryManager.Instance != null ? InventoryManager.Instance.TotalBonusHp : 0f;
    public float BonusDamage => InventoryManager.Instance != null ? InventoryManager.Instance.TotalBonusDamage : 0f;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp + BonusHp;
    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;

    private void Start()
    {
        networkObject = GetComponent<NetworkObject>();
        networkObject.Initialize(System.Guid.NewGuid().ToString(), true);
        characterController = GetComponent<CharacterController>();
        movementController = GetComponent<IsometricPlayerController>();
        mainCamera = Camera.main;

        currentHp = MaxHp;
        currentMana = maxMana;

        InitializeSkillSlots();
        EnsureEventSystemExists();

        if (InventoryManager.Instance == null)
        {
            gameObject.AddComponent<InventoryManager>();
        }
        else
        {
            _ = InventoryManager.Instance.InitializePlayerInventoryAsync();
        }

        if (GetComponent<InventoryUIController>() == null)
        {
            gameObject.AddComponent<InventoryUIController>();
        }

        if (NetworkSessionManager.Instance == null)
        {
            gameObject.AddComponent<NetworkSessionManager>();
        }

        if (GetComponent<SocialUIController>() == null)
        {
            gameObject.AddComponent<SocialUIController>();
        }
    }

    private void EnsureEventSystemExists()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void InitializeSkillSlots()
    {
        while (equippedSkills.Count < 6)
        {
            equippedSkills.Add(null);
        }

        if (basicAttackSkill != null && equippedSkills[0] == null)
        {
            equippedSkills[0] = basicAttackSkill;
        }

        string[] defaultKeys = { "basic_slash", "heavy_pierce", "blade_vortex", "shadow_step", "crimson_nova", "phantom_dash" };
        string[] defaultNames = { "Golpe Básico", "Estocada Sombria", "Vórtice de Lâminas", "Passo das Sombras", "Nova Carmesim", "Avanço Fantasma" };
        SkillCategory[] defaultCategories = {
            SkillCategory.DirectionalStrike,
            SkillCategory.DirectionalStrike,
            SkillCategory.CircularAoE,
            SkillCategory.MobilityDash,
            SkillCategory.CircularAoE,
            SkillCategory.MobilityDash
        };
        float[] defaultCooldowns = { 0.45f, 2.0f, 3.5f, 4.0f, 6.0f, 3.0f };
        float[] defaultManaCosts = { 0f, 10f, 15f, 10f, 25f, 15f };
        float[] defaultDamages = { 20f, 35f, 30f, 15f, 50f, 0f };
        float[] defaultRanges = { 2.5f, 3.2f, 3.5f, 4.5f, 4.5f, 5.0f };
        float[] defaultDashDurations = { 0f, 0f, 0f, 0.18f, 0f, 0.15f };

        for (int i = 0; i < 6; i++)
        {
            if (equippedSkills[i] == null)
            {
                SkillData runtimeSkill = ScriptableObject.CreateInstance<SkillData>();
                runtimeSkill.name = defaultNames[i];
                runtimeSkill.skillKey = defaultKeys[i];
                runtimeSkill.displayName = defaultNames[i];
                runtimeSkill.category = defaultCategories[i];
                runtimeSkill.cooldown = defaultCooldowns[i];
                runtimeSkill.manaCost = defaultManaCosts[i];
                runtimeSkill.damage = defaultDamages[i];
                runtimeSkill.range = defaultRanges[i];
                runtimeSkill.dashDuration = defaultDashDurations[i];
                equippedSkills[i] = runtimeSkill;
            }

            skillMaxCooldowns[i] = Mathf.Max(equippedSkills[i].cooldown, 0.01f);
        }
    }

    private void Update()
    {
        UpdateCooldowns();
        RegenerateResources();
        HandleCombatInputs();
    }

    private void UpdateCooldowns()
    {
        for (int i = 0; i < skillCooldowns.Length; i++)
        {
            if (skillCooldowns[i] > 0f)
            {
                skillCooldowns[i] -= Time.deltaTime;
                if (skillCooldowns[i] < 0f)
                {
                    skillCooldowns[i] = 0f;
                }
            }
        }
    }

    private void RegenerateResources()
    {
        if (currentHp > 0f && currentHp < MaxHp)
        {
            currentHp = Mathf.Min(MaxHp, currentHp + hpRegenRate * Time.deltaTime);
        }

        if (currentMana < maxMana)
        {
            currentMana = Mathf.Min(maxMana, currentMana + manaRegenRate * Time.deltaTime);
        }
    }

    private void HandleCombatInputs()
    {
        if (InventoryUIController.IsPointerOverInventory || SocialUIController.IsPointerOverSocial) return;

        if (Mouse.current != null)
        {
            if (!WorldItemDrop.IsHoveringAnyItem && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryCastSkill(0, GetSkillInSlot(0));
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                TryCastSkill(1, GetSkillInSlot(1));
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
                TryCastSkill(2, GetSkillInSlot(2));

            if (Keyboard.current.eKey.wasPressedThisFrame)
                TryCastSkill(3, GetSkillInSlot(3));

            if (Keyboard.current.rKey.wasPressedThisFrame)
                TryCastSkill(4, GetSkillInSlot(4));

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                TryCastSkill(5, GetSkillInSlot(5));
        }
    }

    public void TryCastSkill(int slotIndex, SkillData skillData)
    {
        if (slotIndex < 0 || slotIndex >= skillCooldowns.Length || skillData == null) return;

        if (skillData.category == SkillCategory.MobilityDash && isDashing) return;

        if (skillCooldowns[slotIndex] > 0f)
        {
            Debug.Log($"[Combate] {skillData.displayName} em recarga: {skillCooldowns[slotIndex]:F1}s");
            return;
        }

        if (currentMana < skillData.manaCost)
        {
            Debug.Log($"[Combate] Mana insuficiente para {skillData.displayName}!");
            return;
        }

        currentMana -= skillData.manaCost;
        skillCooldowns[slotIndex] = skillData.cooldown;
        skillMaxCooldowns[slotIndex] = Mathf.Max(skillData.cooldown, 0.01f);

        ExecuteSkill(slotIndex, skillData);
    }

    public void ExecuteSkill(int slotIndex, SkillData skillData)
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 direction = mouseWorldPos - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
            transform.forward = direction;
        }
        else
        {
            direction = transform.forward;
        }

        switch (skillData.category)
        {
            case SkillCategory.DirectionalStrike:
                Vector3 strikeOrigin = transform.position + direction * (skillData.range * 0.6f);
                ExecuteDirectionalStrike(strikeOrigin, direction, skillData);
                break;

            case SkillCategory.CircularAoE:
                ExecuteCircularAoE(transform.position, skillData);
                break;

            case SkillCategory.MobilityDash:
                StartCoroutine(ExecuteMobilityDashRoutine(direction, skillData));
                break;
        }
    }

    public void ExecuteDirectionalStrike(Vector3 origin, Vector3 direction, SkillData skillData)
    {
        float radius = Mathf.Max(skillData.range * 0.65f, 1.2f);
        SpawnSkillVisual(origin, radius, skillData, new Color(1f, 0.85f, 0.2f, 0.6f));

        Collider[] hits = Physics.OverlapSphere(origin, radius);
        ApplyDamageToHits(hits, skillData.damage + BonusDamage, origin);
    }

    public void ExecuteCircularAoE(Vector3 center, SkillData skillData)
    {
        float radius = Mathf.Max(skillData.range, 2.0f);
        SpawnSkillVisual(center, radius, skillData, new Color(0.85f, 0.2f, 0.3f, 0.5f));

        Collider[] hits = Physics.OverlapSphere(center, radius);
        ApplyDamageToHits(hits, skillData.damage + BonusDamage, center);
    }

    private IEnumerator ExecuteMobilityDashRoutine(Vector3 dashDirection, SkillData skillData)
    {
        isDashing = true;

        // Trava a direção normalizada no plano XZ para não sofrer interferência durante o avanço
        Vector3 lockedDirection = new Vector3(dashDirection.x, 0f, dashDirection.z).normalized;
        if (lockedDirection.sqrMagnitude < 0.001f)
        {
            lockedDirection = transform.forward;
        }

        transform.forward = lockedDirection;

        // Pausa temporariamente o script de movimento WASD durante o Dash para não brigar com o CharacterController
        bool wasMovementEnabled = false;
        if (movementController != null && movementController.enabled)
        {
            wasMovementEnabled = true;
            movementController.enabled = false;
        }

        float duration = Mathf.Max(skillData.dashDuration, 0.12f);
        float distance = Mathf.Max(skillData.range, 3.5f);
        float dashSpeed = distance / duration;
        float elapsed = 0f;

        SpawnSkillVisual(transform.position, 1.2f, skillData, new Color(0.3f, 0.7f, 1f, 0.5f));

        while (elapsed < duration)
        {
            float step = dashSpeed * Time.deltaTime;
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(lockedDirection * step);
            }
            else
            {
                transform.position += lockedDirection * step;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (wasMovementEnabled && movementController != null)
        {
            movementController.enabled = true;
        }

        if (skillData.damage > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 2.0f);
            ApplyDamageToHits(hits, skillData.damage + BonusDamage, transform.position);
        }

        isDashing = false;
    }

    private void SpawnSkillVisual(Vector3 position, float radius, SkillData skillData, Color fallbackColor)
    {
        if (skillData.vfxPrefab != null)
        {
            GameObject vfx = Instantiate(skillData.vfxPrefab, position, Quaternion.LookRotation(transform.forward));
            Destroy(vfx, 1.5f);
            return;
        }

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(indicator.GetComponent<Collider>());
        indicator.transform.position = new Vector3(position.x, transform.position.y + 0.05f, position.z);
        indicator.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        if (indicator.TryGetComponent<Renderer>(out var rend))
        {
            rend.material.color = fallbackColor;
        }

        Destroy(indicator, 0.2f);
    }

    public void ApplyDamageToHits(Collider[] hits, float damage, Vector3 hitPoint)
    {
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<TacticalEnemyAI>(out var enemy))
            {
                enemy.TakeDamage(damage, hitPoint);
            }
        }
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        currentHp -= damage;
        if (currentHp < 0f) currentHp = 0f;

        Debug.Log($"[Player] Recebeu {damage} de dano! HP: {currentHp}/{MaxHp}");

        if (currentHp <= 0f)
        {
            Debug.Log("[Player] Vida zerada! Restaurando HP para continuar o teste...");
            currentHp = MaxHp;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindAnyObjectByType<Camera>();

        if (mainCamera != null && Mouse.current != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            // Plano horizontal matemático na altura do Player: funciona em 360° e nunca colide com o próprio corpo do Player
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (groundPlane.Raycast(ray, out float enterDistance))
            {
                return ray.GetPoint(enterDistance);
            }
        }

        return transform.position + transform.forward * 2f;
    }

    public SkillData GetSkillInSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < equippedSkills.Count)
        {
            return equippedSkills[slotIndex];
        }
        return null;
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < skillCooldowns.Length)
        {
            return skillCooldowns[slotIndex];
        }
        return 0f;
    }

    public float GetCooldownRatio(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= skillCooldowns.Length) return 0f;

        float maxCd = skillMaxCooldowns[slotIndex];
        if (maxCd <= 0f) return 0f;

        return Mathf.Clamp01(skillCooldowns[slotIndex] / maxCd);
    }
}