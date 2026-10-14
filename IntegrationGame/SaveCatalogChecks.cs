using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using ColossalFramework.Packaging;

static class SaveCatalogChecks
{
    public static void Run()
    {
        string folder=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SAVE_CATALOG");
        if(string.IsNullOrEmpty(folder) || !Directory.Exists(folder))throw new Exception("Save catalog source directory missing.");
        var report=new StringBuilder("file\tcity\tpopulation\ttimestamp\n");int count=0;
        foreach(string path in Directory.GetFiles(folder,"*.crp"))
        {
            string before=Hash(path);
            // Temporary packages are never registered with the user's package list.
            var package=new Package(Path.GetFileNameWithoutExtension(path),path,true);
            foreach(var asset in package.FilterAssets((Package.AssetType)UserAssetType.SaveGameMetaData))
            {
                var meta=asset.Instantiate<SaveGameMetaData>();if(meta==null)throw new Exception("Save metadata missing: "+Path.GetFileName(path));
                report.Append(Path.GetFileName(path)).Append('\t').Append(Clean(meta.cityName)).Append('\t').Append(Clean(meta.population)).Append('\t').Append(meta.timeStamp.ToString("o")).Append('\n');count++;
            }
            if(Hash(path)!=before)throw new Exception("Read-only metadata scan changed its input.");
        }
        File.WriteAllText(Path.Combine(Entry.Root,"save-catalog.tsv"),report.ToString());
        Entry.Log("PASS: read-only save metadata catalog; "+count+" cities; all input hashes unchanged. No city loaded or saved.");
    }
    static string Clean(string value){return (value??"").Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');}
    static string Hash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream));}
}
