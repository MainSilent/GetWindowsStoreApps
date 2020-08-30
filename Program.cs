// BCL namespaces
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Xml;
// WinRT namespaces
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.ApplicationModel.Resources.Core;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Collections;
using System.Drawing;

namespace GetWindowsStoreApps  {
class App
{
    public int id { get; set; }
    public string Name { get; set; }
    public string AppId { get; set; }
    public string PFN { get; set; }
    public string Icon { get; set; }
    public string unplatedIcon { get; set; }
    public string lightUnplatedIcon { get; set; }
    public int DateCreated { get; set; }
}
class Program
{
    [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
    public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

    static string GetOutBuff(string ManifestString) 
    {
        StringBuilder outBuff = new StringBuilder(1024);
        int result = SHLoadIndirectString(ManifestString, outBuff, outBuff.Capacity, IntPtr.Zero);
        return outBuff.ToString();
    }
    static string GetDisplayName(string DisplayName, string idName, string Path)
    {
        if (!DisplayName.StartsWith("ms-resource:"))
        {
            return DisplayName;
        }
        // if the DisplayName came from a resource
        else
        {
            string trimmedDisplayName = DisplayName;
            while (trimmedDisplayName.StartsWith("ms-resource:"))
            {
                trimmedDisplayName = trimmedDisplayName.Substring("ms-resource:".Length);
            }

            string ManifestString = "@{" + Path + "\\resources.pri?ms-resource://" + idName + "/resources/" + trimmedDisplayName + "}";
            string res = GetOutBuff(ManifestString);

            if (res == "") ManifestString = "@{" + Path + "\\resources.pri?" + DisplayName + "}";
            res = GetOutBuff(ManifestString);

            return res;
        }
    }

    static void Main(string[] args)
    {
        PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
        IEnumerable<Package> packages = 
            packageManager.FindPackagesForUser(WindowsIdentity.GetCurrent().User.Value);

        int key = 1;
        var applist = new List<App> { };
        foreach (Package package in packages)
        {
            try
            {
                if (!package.IsFramework && package.InstalledLocation.Path.StartsWith("C:\\Program Files\\WindowsApps"))
                {
                    // ---- Package Family Name
                    //package.Id.FamilyName

                    // ---- AppManifest(AppId)
                    XmlDocument doc = new XmlDocument();
                    string AppManifest = System.IO.Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                    doc.Load(AppManifest);
                    XmlNodeList Application = doc.GetElementsByTagName("Application");
                    string AppId = Application[0].Attributes["Id"].Value;
                    //AppId

                    // ---- Get DisplayName
                    XmlNodeList VisualElements = doc.GetElementsByTagName("uap:VisualElements");
                    string DisplayName = VisualElements[0].Attributes["DisplayName"].Value;
                    string FinalDisplayName = GetDisplayName(DisplayName, package.Id.Name, package.InstalledLocation.Path);
                    //FinalDisplayName

                    // ---- Get IconPath
                    int max_Icon_Size = 0;
                    int max_Unplated_Icon_Size = 0;
                    int max_Light_Unplated_Icon_Size = 0;
                    string iconPath = "";
                    string unplated_Icon_Path = null;
                    string light_Unplated_Icon_Path = null;
                    string icon44x44 = VisualElements[0].Attributes["Square44x44Logo"].Value;
                    string baseIcon44x44 = Path.GetDirectoryName(icon44x44);
                    string finalIcon44x44Dir = Path.Combine(package.InstalledLocation.Path, baseIcon44x44);
                    string icon44x44BaseName = Path.GetFileNameWithoutExtension(icon44x44);
                    var dirFileList = Directory.GetFiles(finalIcon44x44Dir);

                    foreach(string file in dirFileList) 
                    {
                        string fileBaseName = Path.GetFileNameWithoutExtension(file);
                        if (fileBaseName.Contains(icon44x44BaseName)) 
                        {
                            // Light Unplated Images
                            if (fileBaseName.Contains("altform-lightunplated") && !fileBaseName.Contains("contrast-black") && !fileBaseName.Contains("contrast-white")) 
                            {
                                string props = fileBaseName.Split('.')[1];
                                int size = Convert.ToInt32(props.Split('_')[0].Split('-')[1]);
                                if (size > max_Light_Unplated_Icon_Size)
                                {
                                    max_Light_Unplated_Icon_Size = size;
                                    light_Unplated_Icon_Path = file;
                                }
                            }
                            // Unplated Images
                            else if (fileBaseName.Contains("altform-unplated") && !fileBaseName.Contains("contrast-black") && !fileBaseName.Contains("contrast-white"))
                            {
                                string props = fileBaseName.Split('.')[1];
                                int size = Convert.ToInt32(props.Split('_')[0].Split('-')[1]);
                                if (size > max_Unplated_Icon_Size)
                                {
                                    max_Unplated_Icon_Size = size;
                                    unplated_Icon_Path = file;
                                }
                            }
                            // Normal Images
                            else if (!fileBaseName.Contains("contrast-black") && !fileBaseName.Contains("contrast-white"))
                            {
                                string props = fileBaseName.Split('.')[1];
                                int size = Convert.ToInt32(props.Split('_')[0].Split('-')[1]);
                                if (size > max_Icon_Size)
                                {
                                    max_Icon_Size = size;
                                    iconPath = file;
                                }
                            }
                        }
                    }
                    //iconPath && lightModeIconPath

                    // ---- DateCreated
                    DateTime baseDate = new DateTime(1970, 01, 01);
                    var toDate = package.InstalledLocation.DateCreated;
                    double numberOfSeconds = toDate.Subtract(baseDate).TotalSeconds;
                    //numberOfSeconds
                    
                    if (FinalDisplayName != "") 
                    {
                        applist.Add(new App {
                            id = key,
                            Name = FinalDisplayName,
                            AppId = AppId,
                            PFN = package.Id.FamilyName,
                            Icon = iconPath,
                            unplatedIcon = unplated_Icon_Path,
                            lightUnplatedIcon = light_Unplated_Icon_Path,
                            DateCreated = (int)numberOfSeconds
                        });
                        key = key + 1;
                    }
                }
            }
            catch (Exception e) 
            {
                //Console.WriteLine(e);
            }
        }

        string DataPath = args[0];
        if (DataPath == "/test")
        {
            string JsonAppList = JsonConvert.SerializeObject(applist);
            Console.Write(JsonAppList);
        }
        else
        {
            string JsonAppList = JsonConvert.SerializeObject(applist);
            using (StreamWriter JsonFile = File.CreateText(DataPath))
            {
                JsonFile.Write(JsonAppList);
            }
            Console.WriteLine("Processing StoreApps Data Success");
        }
    }
}
}

