using System.Text.RegularExpressions;
using System.Numerics;
namespace MyVoice.Updater;
public static class ReleaseVersion
{
    public static bool IsNewer(string version,string tag,string installed)
    {
        if(!SemanticRelease.TryParse(version,out var core)||core.Prerelease.Length!=0||!SemanticRelease.TryParse(string.IsNullOrEmpty(tag)?version:tag,out var target)||!SemanticRelease.TryParse(installed,out var current))return false;
        if((core.Major,core.Minor,core.Patch)!=(target.Major,target.Minor,target.Patch))return false;
        return target.CompareTo(current)>0;
    }
}

/// <summary>Semantic Versioning 2.0 precedence; build metadata does not affect ordering.</summary>
public sealed record SemanticRelease(BigInteger Major,BigInteger Minor,BigInteger Patch,string[] Prerelease) : IComparable<SemanticRelease>
{
    public static bool TryParse(string? value,out SemanticRelease result)
    {
        result=null!;
        if(value==null||value.Length>256)return false;
        var m=Regex.Match(value,@"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$");
        if(!m.Success)return false;
        var pre=m.Groups[4].Success?m.Groups[4].Value.Split('.'):[];
        if(pre.Any(s=>s.Length>1&&s[0]=='0'&&s.All(char.IsAsciiDigit)))return false;
        result=new(BigInteger.Parse(m.Groups[1].Value),BigInteger.Parse(m.Groups[2].Value),BigInteger.Parse(m.Groups[3].Value),pre);return true;
    }
    public int CompareTo(SemanticRelease? other)
    {
        if(other==null)return 1;
        foreach(var pair in new[]{(Major,other.Major),(Minor,other.Minor),(Patch,other.Patch)}){var c=pair.Item1.CompareTo(pair.Item2);if(c!=0)return c;}
        if(Prerelease.Length==0||other.Prerelease.Length==0)return Prerelease.Length==other.Prerelease.Length?0:Prerelease.Length==0?1:-1;
        for(var i=0;i<Math.Min(Prerelease.Length,other.Prerelease.Length);i++)
        {
            var a=Prerelease[i];var b=other.Prerelease[i];var an=a.All(char.IsAsciiDigit);var bn=b.All(char.IsAsciiDigit);
            var c=an&&bn?BigInteger.Parse(a).CompareTo(BigInteger.Parse(b)):an!=bn?an?-1:1:string.CompareOrdinal(a,b);
            if(c!=0)return c;
        }
        return Prerelease.Length.CompareTo(other.Prerelease.Length);
    }
}
