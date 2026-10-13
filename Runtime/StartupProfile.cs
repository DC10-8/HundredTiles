using System;
using System.IO;
using ColossalFramework.IO;

namespace HundredTiles.Runtime
{
    public static class StartupProfile
    {
        static int selected=-1;
        static string FilePath {get{return Path.Combine(DataLocation.localApplicationData,"HundredTiles.startup-profile.txt");}}
        public static int Selected
        {
            get
            {
                if(selected<0)selected=File.Exists(FilePath) && File.ReadAllText(FilePath).Trim()=="editor"?1:0;
                return selected;
            }
        }
        // Selection affects the next process, never already allocated grids.
        public static void Set(int value)
        {
            if(value!=0 && value!=1)throw new ArgumentOutOfRangeException("value");
            File.WriteAllText(FilePath,value==1?"editor":"city");
            selected=value;
        }
    }
}
