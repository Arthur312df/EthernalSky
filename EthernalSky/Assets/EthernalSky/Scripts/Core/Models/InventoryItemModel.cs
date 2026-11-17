using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("inventory")]
public class InventoryItemModel : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; }

    [Column("character_id")]
    public string CharacterId { get; set; }

    [Column("item_key")]
    public string ItemKey { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    [Column("slot_index")]
    public int SlotIndex { get; set; }

    [Column("metadata")]
    public string Metadata { get; set; } = "{}";
}