using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("profiles")]
public class ProfileModel : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; }

    [Column("username")]
    public string Username { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}