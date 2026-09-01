using System.Globalization;
using System.Resources;
using System.Threading;

namespace ProjectIndexer.Wpf.Resources;

public static class Strings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en");
    
    public static CultureInfo CurrentCulture => _currentCulture;
    
    public static event Action? CultureChanged;
    
    static Strings()
    {
        _resourceManager = new ResourceManager("ProjectIndexer.Wpf.Resources.Strings", typeof(Strings).Assembly);
    }
    
    public static void SetCulture(string cultureName)
    {
        try
        {
            _currentCulture = CultureInfo.GetCultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            CultureChanged?.Invoke();
        }
        catch
        {
            _currentCulture = CultureInfo.GetCultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            Thread.CurrentThread.CurrentCulture = _currentCulture;
        }
    }
    
    public static string Get(string key)
    {
        try
        {
            return _resourceManager?.GetString(key, _currentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }
    
    public static string Get(string key, params object[] args)
    {
        try
        {
            var format = _resourceManager?.GetString(key, _currentCulture) ?? key;
            return string.Format(_currentCulture, format, args);
        }
        catch
        {
            return key;
        }
    }
    
    public static string AppTitle => Get("AppTitle");
    public static string Ready => Get("Ready");
    public static string Search => Get("Search");
    public static string SearchPlaceholder => Get("SearchPlaceholder");
    public static string IndexAllDrives => Get("IndexAllDrives");
    public static string LoadFromDatabase => Get("LoadFromDatabase");
    public static string CancelIndexing => Get("CancelIndexing");
    public static string ClearFolder => Get("ClearFolder");
    public static string FolderPath => Get("FolderPath");
    public static string MigrateToFastIndex => Get("MigrateToFastIndex");
    public static string ShowArchives => Get("ShowArchives");
    public static string RefreshArchives => Get("RefreshArchives");
    public static string OpenFile => Get("OpenFile");
    public static string OpenFolder => Get("OpenFolder");
    public static string CopyPath => Get("CopyPath");
    public static string CopyName => Get("CopyName");
    public static string Name => Get("Name");
    public static string Path => Get("Path");
    public static string Size => Get("Size");
    public static string Modified => Get("Modified");
    public static string Created => Get("Created");
    public static string Type => Get("Type");
    public static string Extension => Get("Extension");
    public static string Attributes => Get("Attributes");
    public static string Source => Get("Source");
    public static string TotalFiles(int count) => Get("TotalFiles", count);
    public static string TotalDirectories(int count) => Get("TotalDirectories", count);
    public static string ArchiveCount(int count) => Get("ArchiveCount", count);
    public static string TotalArchiveSize(string size) => Get("TotalArchiveSize", size);
    public static string Indexing(char drive) => Get("Indexing", drive);
    public static string IndexingCompleted(long count) => Get("IndexingCompleted", count);
    public static string ArchiveSaving => Get("ArchiveSaving");
    public static string ArchiveSaved(string name) => Get("ArchiveSaved", name);
    public static string ArchiveLoading => Get("ArchiveLoading");
    public static string ArchiveLoaded(long count) => Get("ArchiveLoaded", count);
    public static string AdminRequired => Get("AdminRequired");
    public static string Language => Get("Language");
    public static string English => Get("English");
    public static string Persian => Get("Persian");
    public static string Arabic => Get("Arabic");
    public static string Turkish => Get("Turkish");
    public static string Settings => Get("Settings");
    public static string SelectDrives => Get("SelectDrives");
    public static string Drive => Get("Drive");
    public static string FileSystem => Get("FileSystem");
    public static string Status => Get("Status");
    public static string Indexable => Get("Indexable");
    public static string NotIndexable => Get("NotIndexable");
    public static string NeedsAdmin => Get("NeedsAdmin");
    public static string FreeSpace => Get("FreeSpace");
    public static string TotalSize => Get("TotalSize");
    public static string SelectArchive => Get("SelectArchive");
    public static string ArchiveName => Get("ArchiveName");
    public static string CreatedAt => Get("CreatedAt");
    public static string EntryCount => Get("EntryCount");
    public static string FileSize => Get("FileSize");
    public static string DriveLabel => Get("DriveLabel");
    public static string LoadArchive => Get("LoadArchive");
    public static string DeleteArchive => Get("DeleteArchive");
    public static string ConfirmDelete => Get("ConfirmDelete");
    public static string Error => Get("Error");
    public static string Success => Get("Success");
    public static string Warning => Get("Warning");
    public static string Information => Get("Information");
    public static string NoArchivesFound => Get("NoArchivesFound");
    public static string NoIndexesLoaded => Get("NoIndexesLoaded");
    public static string SearchResults(long count) => Get("SearchResults", count);
    public static string ShowingFirst(int showing, int total) => Get("ShowingFirst", showing, total);
    public static string IndexingCancelled => Get("IndexingCancelled");
    public static string PathCopied => Get("PathCopied");
    public static string NameCopied => Get("NameCopied");
    public static string MigratingIndexes => Get("MigratingIndexes");
    public static string MigrationCompleted => Get("MigrationCompleted");
    public static string LoadingIndexes => Get("LoadingIndexes");
    public static string ArchiveLoadCancelled => Get("ArchiveLoadCancelled");
}