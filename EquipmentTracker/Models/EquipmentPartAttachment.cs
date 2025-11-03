using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EquipmentTracker.Models
{
    public class EquipmentPartAttachment
    {
        public int Id { get; set; }


        public string FileName { get; set; }


        public string FilePath { get; set; }

        public int EquipmentPartId { get; set; }
        [JsonIgnore]
        public EquipmentPart EquipmentPart { get; set; }
    }
}
