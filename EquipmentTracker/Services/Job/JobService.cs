// Dosya: Services/Job/JobService.cs
using EquipmentTracker.Models;

namespace EquipmentTracker.Services.Job
{
    public class JobService : IJobService
    {
        private readonly List<JobModel> _jobs;

        // Sahte veritabanımızı (constructor) yeni hiyerarşiyle dolduralım
        public JobService()
        {
            _jobs = new List<JobModel>
            {
                // İŞ 1: AŞKALE (Excel'deki detaylı veri)
                new JobModel
                {
                    Id = 1,
                    JobNumber = "001",
                    JobName = "AŞKALE ÇİMENTO PAKET ARITMASI",
                    JobOwner = "STH ÇEVRE",
                    Date = new DateTime(2025, 9, 15),
                    Equipments = new List<Equipment>
                    {
                        new Equipment
                        {
                            EquipmentId = "001", EquipmentCode = "001.001", Name = "KARIŞTIRICI TANKI",
                            Parts = new List<EquipmentPart>
                            {
                                new EquipmentPart { PartId = "1", PartCode = "001.001.1", Name = "TANK GÖVDESİ" },
                                new EquipmentPart { PartId = "2", PartCode = "001.001.2", Name = "DOLU SAVAK" },
                                new EquipmentPart { PartId = "3", PartCode = "001.001.3", Name = "AYAKLARI" }
                            }
                        },
                        new Equipment
                        {
                            EquipmentId = "002", EquipmentCode = "001.002", Name = "PERGEL VİNÇ",
                            Parts = new List<EquipmentPart>
                            {
                                new EquipmentPart { PartId = "1", PartCode = "001.002.1", Name = "TABAN BAĞLANTISI" },
                                new EquipmentPart { PartId = "2", PartCode = "001.002.2", Name = "VİNÇ GÖVDESİ" }
                            }
                        },
                        new Equipment { EquipmentId = "003", EquipmentCode = "001.003", Name = "POMPA" }
                    }
                },
                // İŞ 2: (Gezinti (navigation) çalışıyor mu diye test etmek için)
                new JobModel
                {
                    Id = 2,
                    JobNumber = "002",
                    JobName = "TRABZON SU ARITMA TESİSİ",
                    JobOwner = "TİSKİ",
                    Date = new DateTime(2025, 10, 20),
                    Equipments = new List<Equipment>
                    {
                        new Equipment { EquipmentId = "001", EquipmentCode = "002.001", Name = "ANA GİRİŞ VANA ODASI" }
                    }
                }
            };
        }

        public Task<List<JobModel>> GetAllJobsAsync()
        {
            return Task.FromResult(_jobs);
        }
    }
}