namespace WatchMark.App.Models;

public class LibraryPathItem
{
    public string FolderName { get; set; }
    public string FullPath { get; set; }

    public LibraryPathItem(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            FullPath = string.Empty;
            FolderName = "(Empty)";
            return;
        }
        
        FullPath = fullPath;
        FolderName = System.IO.Path.GetFileName(fullPath.TrimEnd('\\', '/'));
        
        // If folder name is empty (e.g., root like C:\), use the full path
        if (string.IsNullOrWhiteSpace(FolderName))
        {
            FolderName = fullPath;
        }
    }

    public override string ToString() => FolderName;
}
