using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhonePalace.Domain.Interfaces
{
    public interface IBackupService
    {
        Task<string> CreateBackupAsync();
        IEnumerable<BackupFileInfo> GetBackups();
    }

    public class BackupFileInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime CreationTime { get; set; }
        public long Size { get; set; }
    }
}
