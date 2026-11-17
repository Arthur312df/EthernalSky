using UnityEngine;

public enum SkillCategory
{
    DirectionalStrike = 0, // Golpe frontal na direção do cursor
    CircularAoE = 1,       // Dano em área 360° ao redor do personagem
    MobilityDash = 2       // Esquiva ou avanço rápido na direção do cursor
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "EthernalSky/Combat/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identificação (Banco de Dados)")]
    public string skillKey = "basic_slash";
    public string displayName = "Golpe Básico";
    public SkillCategory category = SkillCategory.DirectionalStrike;

    [Header("Atributos de Combate")]
    public float cooldown = 0.8f;
    public float manaCost = 0f;
    public float damage = 15f;
    public float range = 2.5f;
    public float dashDuration = 0.18f;

    [Header("Feedback Visual")]
    public GameObject vfxPrefab;
}