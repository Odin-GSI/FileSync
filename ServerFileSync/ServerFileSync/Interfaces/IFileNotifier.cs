namespace ServerFileSync.Interfaces
{
    public interface IFileNotifier
    {
        void NotifyNewFile(string fileName, string CRC);
        void NotifyDeleteFile(string fileName);
    }
}