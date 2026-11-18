namespace EthernalSky.Models
{
    [Supabase.Postgrest.Attributes.Table("items_inventory")]
    public class InventoryItemModel : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("id")]
        public string Id { get; set; }

        [Supabase.Postgrest.Attributes.Column("character_id")]
        public string CharacterId { get; set; }

        [Supabase.Postgrest.Attributes.Column("item_key")]
        public string ItemKey { get; set; }

        [Supabase.Postgrest.Attributes.Column("quantity")]
        public int Quantity { get; set; }

        [Supabase.Postgrest.Attributes.Column("is_equipped")]
        public bool IsEquipped { get; set; }
    }
}