using UnityEngine;

public enum SkillSlotType
{
    BasicAttack = 0,
    Active1 = 1,
    Active2 = 2,
    Active3 = 3,
    Active4 = 4,
    Active5 = 5
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "EthernalSky/Combat/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identificação (Banco de Dados)")]
    public string skillKey = "basic_slash";
    public string displayName = "Golpe Básico";

    [Header("Atributos de Combate")]
    public float cooldown = 0.8f;
    public float manaCost = 0f;
    public float damage = 15f;
    public float range = 2.5f;

    [Header("Feedback Visual")]
    public GameObject vfxPrefab;
}