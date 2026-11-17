using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("character_skills")]
public class CharacterSkillModel : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; }

    [Column("character_id")]
    public string CharacterId { get; set; }

    [Column("skill_key")]
    public string SkillKey { get; set; }

    [Column("slot_type")]
    public string SlotType { get; set; }

    [Column("slot_index")]
    public int SlotIndex { get; set; }
}