namespace EthernalSky.Models
{
    [Supabase.Postgrest.Attributes.Table("character_skills")]
    public class CharacterSkillModel : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("id")]
        public string Id { get; set; }

        [Supabase.Postgrest.Attributes.Column("character_id")]
        public string CharacterId { get; set; }

        [Supabase.Postgrest.Attributes.Column("skill_key")]
        public string SkillKey { get; set; }

        [Supabase.Postgrest.Attributes.Column("slot_index")]
        public int SlotIndex { get; set; }
    }
}