using System;
using System.IO;
using System.Text;
//using System.Collections.Generic;

namespace UserScript
{
    public class Script : BaseScript
    {
        private static string[] Sizes = { "B", "KB", "MB", "GB", "TB" };
        private static char[] PathTrimChars = { '/' };
        private const int MAX_FILENAME_LENGTH = 48;

        public Script(params object[] args) : base(args)
        {

        }

        public override bool Run()
        {
            Header("content-type", "text/html;charset=UTF-8");

            var url = _SERVER["url"];
            var rawUrl = _SERVER["local_url"];
            var path = "";
            var csi = rawUrl.IndexOf(".cs");
            if (rawUrl.Length > csi + 3)
                path = rawUrl.Substring(csi + 3);

            //Log("Dir: " + path);

            var cleanPath = path.Trim(PathTrimChars);
            if (File.Exists(cleanPath))
            {
                Action(Actions.SendFile);
                Location(new FileInfo(cleanPath).FullName);
                return true;
            }
            else
            {
                if (!path.EndsWith("/"))
                {
                    if (path == string.Empty || Directory.Exists(cleanPath))
                    {
                        Action(Actions.Redirect);
                        Location(url + '/');
                    }
                    else
                    {
                        StatusCode(400);
                    }
                    return true;
                }
                GetHtmlResult(cleanPath + '/');
                return true;
            }
        }

        private bool GetHtmlResult(string path)
        {
            //Console.WriteLine("GET: " + _GET["media"]);            
            if (path == "/")
            {                
                var ds = DriveInfo.GetDrives();
                string[] drives = new string[ds.Length];
                for (int i = 0; i < ds.Length; i++)
                {
                    drives[i] = ds[i].Name;
                }

                GetDirectoryHtml("Index of root", false, drives);
            }
            else if (Directory.Exists(path))
            {
                string modifiedTime = new DirectoryInfo(path).LastWriteTime.ToString("r");
                Header("Last-Modified", modifiedTime);
                
                if (_REQUEST_HEADER["if-modified-since"] == modifiedTime)
                {
                    StatusCode(304); //回應Client不須重新取得資源
                    Action(Actions.None);
                    return true;
                }
                
                if (_GET.Contains("media"))
                {
                    var mediaplayer = GetScript(_SERVER["file-path"] + "mediaplayer.cs", path);
                    if (mediaplayer.Run())
                    {
                        if(mediaplayer.ExtraText != null && mediaplayer.ExtraText.Length > 0)
                        {
                            echo(mediaplayer.ExtraText);
                        }
                    }
                    else StatusCode(404);
                    return true;
                }

                GetDirectoryHtml("Index of " + path, true, Directory.GetFileSystemEntries(path));
            }
            else return false;

            Action(Actions.HTML);
            Header("content-type", "text/html;charset=UTF-8");
            return true;
        }

        private void GetDirectoryHtml(string title, bool back, string[] files)
        {
            echo("<head>");
            echo("<title>" + title + "</title>");
            echo("</head><body>");
            echo("<h1>" + title + "</h1>");
            echo("<table><tbody>");
            if (back)
            {
                echo("<tr><td valign='top'><img src='/icons/back.gif' alt='[PARENTDIR]'></td><td><a href='..'>Parent Directory</a></td><td></td><td align='right'>  - </td><td></td></tr>");
            }
            echo("<tr><th valign='top'></th><th>Name</th><th>Last modified</th><th>Size</th></tr>");
            echo("<tr><th colspan='4'><hr></th></tr>");
            foreach (var file in files)
            {
                if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    var di = new DirectoryInfo(file);
                    var dname = di.Name;
                    if (dname.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        dname = dname.Remove(dname.Length - 1);
                    var shortDname = dname.Length > MAX_FILENAME_LENGTH ?
                        dname.Substring(0, MAX_FILENAME_LENGTH - 3) + "..." :
                        dname;

                    echo("<tr><td valign='top'><img src='/icons/folder.gif' alt='[DIR]'></td><td><a href=\"./");
                    echo(dname + "/\">" + shortDname);
                    echo("/</a></td><td align='right'>");
                    echo(di.LastWriteTime.ToLocalTime());
                    echo("  </td><td align='right'>  - </td></tr>");
                }
                else
                {
                    var fi = new FileInfo(file);
                    var size = FormatFileSize(fi.Length);
                    var ext = fi.Extension;
                    var shortName = fi.Name;
                    if (shortName.Length > MAX_FILENAME_LENGTH)
                        shortName = shortName.Substring(0, MAX_FILENAME_LENGTH - 3 - ext.Length) + "..." + ext;

                    echo("<tr><td valign='top'><img src='/icons/file.gif' alt='[");
                    if (ext.Length > 0) echo(ext.Remove(0, 1).ToUpper());
                    else echo("???");
                    echo("]'></td><td><a href=\"");
                    echo(fi.Name + "\">" + shortName);
                    echo("</a></td><td align='right'>");
                    echo(fi.LastWriteTime.ToLocalTime());
                    echo("  </td><td align='right'>");
                    echo(size);
                    echo("</td></tr>");
                }
            }
            echo("</tbody></table></body>");
        }

        private static string FormatFileSize(long size)
        {
            double len = size;
            int order = 0;
            while (len >= 1024 && order < Sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return Math.Round(len, 1) + Sizes[order];
        }

    }
}
