namespace EthernalSky.Models
{
    [Supabase.Postgrest.Attributes.Table("characters")]
    public class CharacterModel : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("id")]
        public string Id { get; set; }

        [Supabase.Postgrest.Attributes.Column("user_id")]
        public string UserId { get; set; }

        [Supabase.Postgrest.Attributes.Column("name")]
        public string Name { get; set; }

        [Supabase.Postgrest.Attributes.Column("current_hp")]
        public float CurrentHp { get; set; }

        [Supabase.Postgrest.Attributes.Column("max_hp")]
        public float MaxHp { get; set; }

        [Supabase.Postgrest.Attributes.Column("current_mana")]
        public float CurrentMana { get; set; }

        [Supabase.Postgrest.Attributes.Column("max_mana")]
        public float MaxMana { get; set; }

        [Supabase.Postgrest.Attributes.Column("pos_x")]
        public float PosX { get; set; }

        [Supabase.Postgrest.Attributes.Column("pos_y")]
        public float PosY { get; set; }

        [Supabase.Postgrest.Attributes.Column("pos_z")]
        public float PosZ { get; set; }
    }
}