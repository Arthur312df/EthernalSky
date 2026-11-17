using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : NetworkObject, IDamageable
{
    [Header("Loadout de Combate (1 Básico + 5 Skills)")]
    [SerializeField] private SkillData basicAttack;
    [SerializeField] private SkillData[] activeSkills = new SkillData[5];

    [Header("Referências de Disparo")]
    [SerializeField] private Transform castPoint;

    [Header("Regeneração Passiva")]
    [SerializeField] private float hpRegenPerSecond = 2f;
    [SerializeField] private float manaRegenPerSecond = 5f;

    private float[] cooldownTimers = new float[6];

    public float CurrentHp { get; private set; } = 100f;
    public float MaxHp { get; private set; } = 100f;
    public float CurrentMana { get; private set; } = 50f;
    public float MaxMana { get; private set; } = 50f;

   private void Start()
    {
        Initialize(System.Guid.NewGuid().ToString(), true);

        if (CharacterDataManager.Instance != null && CharacterDataManager.Instance.CurrentCharacter != null)
        {
            var data = CharacterDataManager.Instance.CurrentCharacter;
            CurrentHp = data.CurrentHp;
            MaxHp = data.MaxHp;
            CurrentMana = data.CurrentMana;
            MaxMana = data.MaxMana;
        }

        // Sincroniza as skills salvas no Supabase com os slots do loadout
        SyncSkillsFromDatabase();
    }

    private void SyncSkillsFromDatabase()
    {
        if (CharacterDataManager.Instance == null) return;

        // Cria uma lista com todas as skills disponíveis no projeto (você pode arrastar para cá ou buscar via Resources)
        SkillData[] allSkills = Resources.LoadAll<SkillData>("Skills"); // Ou carregadas via referência

        foreach (var dbSkill in CharacterDataManager.Instance.CurrentSkills)
        {
            SkillData matched = System.Array.Find(allSkills, s => s.skillKey == dbSkill.SkillKey);
            if (matched == null) continue;

            if (dbSkill.SlotIndex == 0)
                basicAttack = matched;
            else if (dbSkill.SlotIndex >= 1 && dbSkill.SlotIndex <= 5)
                activeSkills[dbSkill.SlotIndex - 1] = matched;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateCooldowns();
        UpdateRegeneration();
        HandleCombatInputs();
    }

    private void UpdateCooldowns()
    {
        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0f)
            {
                cooldownTimers[i] = Mathf.Max(0f, cooldownTimers[i] - Time.deltaTime);
            }
        }
    }

    private void UpdateRegeneration()
    {
        // Regenera HP passivamente se não estiver com a vida cheia
        if (CurrentHp < MaxHp && CurrentHp > 0f)
        {
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + hpRegenPerSecond * Time.deltaTime);
        }

        // Regenera Mana passivamente se não estiver com a mana cheia
        if (CurrentMana < MaxMana)
        {
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + manaRegenPerSecond * Time.deltaTime);
        }
    }

    private void HandleCombatInputs()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        if (Mouse.current.leftButton.isPressed)
            TryCastSkill(0, basicAttack);

        if (Mouse.current.rightButton.wasPressedThisFrame && activeSkills.Length > 0)
            TryCastSkill(1, activeSkills[0]);

        if (Keyboard.current.qKey.wasPressedThisFrame && activeSkills.Length > 1)
            TryCastSkill(2, activeSkills[1]);

        if (Keyboard.current.eKey.wasPressedThisFrame && activeSkills.Length > 2)
            TryCastSkill(3, activeSkills[2]);

        if (Keyboard.current.rKey.wasPressedThisFrame && activeSkills.Length > 3)
            TryCastSkill(4, activeSkills[3]);

        if (Keyboard.current.spaceKey.wasPressedThisFrame && activeSkills.Length > 4)
            TryCastSkill(5, activeSkills[4]);
    }

    private void TryCastSkill(int slotIndex, SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning($"[Combate] Botão do Slot {slotIndex} pressionado, mas nenhum SkillData foi arrastado para esse slot no Inspector!");
            return;
        }

        if (cooldownTimers[slotIndex] > 0f) return;

        if (CurrentMana < skill.manaCost)
        {
            Debug.Log($"[Combate] Mana insuficiente para {skill.displayName}!");
            return;
        }

        CurrentMana -= skill.manaCost;
        cooldownTimers[slotIndex] = skill.cooldown;

        ExecuteSkill(slotIndex, skill);
    }

    private void ExecuteSkill(int slotIndex, SkillData skill)
    {
        Vector3 origin = castPoint != null ? castPoint.position : transform.position + Vector3.up * 0.5f;
        Vector3 direction = transform.forward;

        if (skill.vfxPrefab != null)
        {
            Instantiate(skill.vfxPrefab, origin, transform.rotation);
        }

        switch (skill.category)
        {
            case SkillCategory.DirectionalStrike:
                ExecuteDirectionalStrike(origin, direction, skill);
                break;

            case SkillCategory.CircularAoE:
                ExecuteCircularAoE(origin, skill);
                break;

            case SkillCategory.MobilityDash:
                ExecuteMobilityDash(origin, direction, skill);
                break;
        }
    }

    private void ExecuteDirectionalStrike(Vector3 origin, Vector3 direction, SkillData skill)
    {
        Vector3 boxCenter = origin + direction * (skill.range * 0.5f);
        Vector3 boxHalfExtents = new Vector3(0.6f, 0.8f, skill.range * 0.5f);

        if (skill.vfxPrefab == null)
        {
            GameObject debugBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugBox.transform.position = boxCenter;
            debugBox.transform.rotation = transform.rotation;
            debugBox.transform.localScale = boxHalfExtents * 2f;
            Destroy(debugBox.GetComponent<Collider>());
            Destroy(debugBox, 0.15f);
        }

        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation);
        ApplyDamageToHits(hits, skill.damage, direction);
    }

   private void ExecuteCircularAoE(Vector3 origin, SkillData skill)
    {
        // Gira o jogador instantaneamente para a posição do mouse antes de soltar a AoE
        if (Camera.main != null && Mouse.current != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 lookDir = hitPoint - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        if (skill.vfxPrefab == null)
        {
            GameObject debugCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            debugCylinder.transform.position = new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z);
            debugCylinder.transform.localScale = new Vector3(skill.range * 2f, 0.05f, skill.range * 2f);
            Destroy(debugCylinder.GetComponent<Collider>());
            Destroy(debugCylinder, 0.2f);
        }

        Collider[] hits = Physics.OverlapSphere(origin, skill.range);
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.TryGetComponent<IDamageable>(out IDamageable target))
            {
                Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                target.TakeDamage(skill.damage, pushDir);
            }
        }
    }

    private void ExecuteMobilityDash(Vector3 origin, Vector3 direction, SkillData skill)
    {
        if (TryGetComponent<IsometricPlayerController>(out IsometricPlayerController movement))
        {
            movement.PerformDash(direction, skill.range, skill.dashDuration);
        }

        if (skill.damage > 0f)
        {
            Vector3 boxCenter = origin + direction * (skill.range * 0.5f);
            Vector3 boxHalfExtents = new Vector3(0.7f, 0.8f, skill.range * 0.5f);
            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation);
            ApplyDamageToHits(hits, skill.damage, direction);
        }
    }

    private void ApplyDamageToHits(Collider[] hits, float damage, Vector3 direction)
    {
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.TryGetComponent<IDamageable>(out IDamageable target))
            {
                target.TakeDamage(damage, direction);
            }
        }
    }

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        Debug.Log($"[Player] Recebeu {amount} de dano! HP: {CurrentHp}/{MaxHp}");

        if (CurrentHp <= 0f)
        {
            Debug.Log("[Player] Vida zerada! Restaurando HP para continuar o teste...");
            CurrentHp = MaxHp;
        }
    }

    public SkillData GetSkillInSlot(int slotIndex)
    {
        if (slotIndex == 0) return basicAttack;
        int activeIndex = slotIndex - 1;
        if (activeIndex >= 0 && activeIndex < activeSkills.Length)
            return activeSkills[activeIndex];
        return null;
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownTimers.Length) return 0f;
        return cooldownTimers[slotIndex];
    }

    public float GetCooldownRatio(int slotIndex)
    {
        SkillData skill = GetSkillInSlot(slotIndex);
        if (skill == null || skill.cooldown <= 0f) return 0f;
        return Mathf.Clamp01(cooldownTimers[slotIndex] / skill.cooldown);
    }

    public void SyncSkillsWithDatabase(List<SkillData> allAvailableSkills)
    {
        if (CharacterDataManager.Instance == null) return;

        foreach (var dbSkill in CharacterDataManager.Instance.CurrentSkills)
        {
            SkillData matchedSkill = allAvailableSkills.Find(s => s.skillKey == dbSkill.SkillKey);
            if (matchedSkill == null) continue;

            if (dbSkill.SlotIndex == 0)
                basicAttack = matchedSkill;
            else if (dbSkill.SlotIndex >= 1 && dbSkill.SlotIndex <= 5)
                activeSkills[dbSkill.SlotIndex - 1] = matchedSkill;
        }
    }
}