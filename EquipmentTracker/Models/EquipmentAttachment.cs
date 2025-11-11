// Dosya: Models/EquipmentAttachment.cs
using System.Text.Json.Serialization;

namespace EquipmentTracker.Models
{

    public class EquipmentAttachment
    {
        public int Id { get; set; }


        public string FileName { get; set; }


        public string FilePath { get; set; }

        /// <summary>
        /// Varsa, bu dosyanın küçük resminin (.png) diskteki yolu.
        /// </summary>
        public string ThumbnailPath { get; set; }

        public int EquipmentId { get; set; }
        [JsonIgnore]
        public Equipment Equipment { get; set; }
    }
}