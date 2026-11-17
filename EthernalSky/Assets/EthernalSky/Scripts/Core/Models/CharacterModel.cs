using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("characters")]
public class CharacterModel : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("experience")]
    public long Experience { get; set; } = 0;

    [Column("current_hp")]
    public float CurrentHp { get; set; } = 100f;

    [Column("max_hp")]
    public float MaxHp { get; set; } = 100f;

    [Column("current_mana")]
    public float CurrentMana { get; set; } = 50f;

    [Column("max_mana")]
    public float MaxMana { get; set; } = 50f;

    [Column("pos_x")]
    public float PosX { get; set; }

    [Column("pos_y")]
    public float PosY { get; set; }

    [Column("pos_z")]
    public float PosZ { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}