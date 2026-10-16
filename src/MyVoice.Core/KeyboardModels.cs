namespace MyVoice.Core;
public sealed record KeyboardSettings
{
    public bool Enabled {get;init;}
    public string Pack {get;init;}="red";
    public double Volume {get;init;}=0.7;
    public double Pitch {get;init;}
    public double Intensity {get;init;}=0.5;
    public double SpacebarVolume {get;init;}=0.8;
    public bool Repeat {get;init;}=false;
    public bool ExcludeModifiers {get;init;}
    public bool ExcludeFunctionKeys {get;init;}
}
public sealed record KeyboardPack
{
    public string Id {get;init;}="";
    public string Name {get;init;}="";
    public string Type {get;init;}="";
    public string Color {get;init;}="#808080";
    public string Author {get;init;}="MyVoice";
    public string Version {get;init;}="1";
    public double DefaultVolume {get;init;}=0.7;
    public Dictionary<string,string[]> Samples {get;init;}=[];
}
public enum KeyboardKeyKind {Normal,Space,Enter,Backspace,Shift,Modifier,Small}
