using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : NetworkObject
{
    [Header("Loadout de Combate (1 Básico + 5 Skills)")]
    [SerializeField] private SkillData basicAttack;
    [SerializeField] private SkillData[] activeSkills = new SkillData[5];

    [Header("Referências de Disparo")]
    [SerializeField] private Transform castPoint;

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
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateCooldowns();
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
        Vector3 boxCenter = origin + direction * (skill.range * 0.5f);
        Vector3 boxHalfExtents = new Vector3(0.6f, 0.8f, skill.range * 0.5f);

        if (skill.vfxPrefab != null)
        {
            Instantiate(skill.vfxPrefab, origin, transform.rotation);
        }
        else
        {
            GameObject debugHitbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugHitbox.transform.position = boxCenter;
            debugHitbox.transform.rotation = transform.rotation;
            debugHitbox.transform.localScale = boxHalfExtents * 2f;

            Destroy(debugHitbox.GetComponent<Collider>());
            Destroy(debugHitbox, 0.15f);
        }

        Collider[] hitColliders = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation);
        foreach (Collider hit in hitColliders)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.TryGetComponent<IDamageable>(out IDamageable target))
            {
                target.TakeDamage(skill.damage, direction);
            }
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