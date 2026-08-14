using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Discord.Wrapper
{
    public partial class StorageManager
    {
        public IEnumerable<FileStat> Files()
        {
            int fileCount = Count();
            List<FileStat> files = new List<FileStat>();
            for (int i = 0; i < fileCount; i++)
            {
                files.Add(StatAt(i));
            }

            return files;
        }
    }
}
