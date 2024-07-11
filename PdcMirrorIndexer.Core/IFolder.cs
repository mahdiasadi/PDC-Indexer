using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace PdcMirrorIndexer
{
    public interface IFolder
    {

        #region Files

        FileInDatabase[] Files { // aby uniemo�liwi� bezpo�rednie operacje na li�cie
            get;
        }
        
        void RemoveFromFiles(FileInDatabase file);

        void AddToFiles(FileInDatabase file);

        int FileCount { // aby skr�ci� czas wykonania Files.Length
            get;
        }

        #endregion

        #region Folders

        IFolder[] Folders {
            get;
        }
        
        void RemoveFromFolders(IFolder folder);

        void AddToFolders(IFolder folder);

        #endregion

        //string GetPath();

        //void GetPath(List<ItemInDatabase> pathList);

        //string GetVolumeUserName();

        //string Name {
        //    get;
        //}

        void CopyToNode(TreeNode treeNode);

        long GetFilesSize();

        /// <summary>
        /// Odnajduje tylko pliki (bez folder�w).
        /// </summary>
        void InsertFilesToList(Regex regex, DateTime? dateFrom, DateTime? dateTo, long? sizeFrom, long? sizeTo, KeywordMatcher keywordMatcher, List<FileInDatabase> listCrc, List<FileInDatabase> listNoCrc);

        void InsertFilesToList(Regex regex, DateTime? dateFrom, DateTime? dateTo, long? sizeFrom, long? sizeTo, KeywordMatcher keywordMatcher, List<ItemInDatabase> list);

        //void RemoveFromAllLogicalFolders();
    }
}
