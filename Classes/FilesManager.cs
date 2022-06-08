﻿using System;
using System.IO;
using System.Windows.Forms;

namespace Widgets.Manager
{
    static class FilesManager
    {
        static public string assetsPath = Application.StartupPath + "/Assets";
        static public string widgetsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Widgets");

        static public string[] GetPathToHTMLFiles(string path)
        {
            return Directory.GetFiles(path, "*.html");
        }

        static public void CreateHTMLFilesDirectory()
        {
            Directory.CreateDirectory(widgetsPath);
        }
    }
}
