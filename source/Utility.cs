using System.IO;

namespace PublisherPlus
{
    public static class Utility
    {
        public static bool IsDirectory(this FileSystemInfo info) => info.Attributes.HasFlag(FileAttributes.Directory);
    }
}
