using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EthernalSky.Models
{
    [Table("friends")]
    public class FriendshipModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("character_id")]
        public string CharacterId { get; set; }

        [Column("friend_name")]
        public string FriendName { get; set; }
    }

    [Table("session_invites")]
    public class SessionInviteModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        [Column("sender_id")]
        public string SenderId { get; set; }

        [Column("sender_name")]
        public string SenderName { get; set; }

        [Column("receiver_name")]
        public string ReceiverName { get; set; }

        [Column("status")]
        public string Status { get; set; }
    }

    [Table("session_players")]
    public class SessionPlayerStateModel : BaseModel
    {
        [PrimaryKey("character_id", true)]
        public string CharacterId { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        [Column("character_name")]
        public string CharacterName { get; set; }

        [Column("pos_x")]
        public float PosX { get; set; }

        [Column("pos_y")]
        public float PosY { get; set; }

        [Column("pos_z")]
        public float PosZ { get; set; }

        [Column("rot_y")]
        public float RotY { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}