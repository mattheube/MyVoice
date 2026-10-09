using System.Text.RegularExpressions;
namespace MyVoice.Updater;
public static class ReleaseVersion
{
    public static bool IsNewer(string version,string tag,string installed)
    {
        installed=installed.Split('+')[0].TrimStart('v');
        var numeric=installed.Split('-')[0];
        if(!Version.TryParse(version,out var target)||!Version.TryParse(numeric,out var current))return false;
        var comparison=target.CompareTo(current);if(comparison!=0)return comparison>0;
        static int? Beta(string value){var m=Regex.Match(value,@"-beta\.([1-9][0-9]*)$");return m.Success&&int.TryParse(m.Groups[1].Value,out var n)?n:null;}
        var from=Beta(installed);var to=Beta(tag);
        if(from==null)return false; // stable is newer than any beta of the same numeric version
        return to==null||to>from;
    }
}
