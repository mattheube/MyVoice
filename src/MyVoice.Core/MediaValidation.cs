using System.Text;
namespace MyVoice.Core;
public static class MediaValidation
{
 public const string AudioFilter="Audio|*.wav;*.flac;*.mp3;*.ogg;*.opus;*.m4a;*.aac";
 public static readonly string[] AudioExtensions=[".wav",".flac",".mp3",".ogg",".opus",".m4a",".aac"];
 public static void RequireAudio(string path){var h=Header(path);if(IsImage(h))throw new InvalidDataException("This file is an image and cannot be used as a voice sample.");var ext=Path.GetExtension(path).ToLowerInvariant();if(!AudioExtensions.Contains(ext))throw new InvalidDataException("Unsupported audio file type.");bool valid=ext switch{".wav"=>At(h,0,"RIFF")&&At(h,8,"WAVE"),".flac"=>At(h,0,"fLaC"),".mp3"=>At(h,0,"ID3")||(h.Length>1&&h[0]==255&&(h[1]&224)==224),".ogg" or ".opus"=>At(h,0,"OggS"),".m4a"=>At(h,4,"ftyp"),".aac"=>h.Length>1&&h[0]==255&&(h[1]&246)==240,_=>false};if(!valid)throw new InvalidDataException("File content does not match a supported audio format.");}
 public static void RequireImage(string path){var ext=Path.GetExtension(path).ToLowerInvariant();if(!new[]{".png",".jpg",".jpeg",".gif",".webp"}.Contains(ext)||!IsImage(Header(path)))throw new InvalidDataException("Only valid PNG, JPEG, GIF or WebP images are accepted.");}
 private static byte[] Header(string path){using var stream=File.OpenRead(path);if(stream.Length==0||stream.Length>2L*1024*1024*1024)throw new InvalidDataException("Empty file or file larger than 2 GB.");var h=new byte[32];var n=stream.Read(h);return h[..n];}
 private static bool At(byte[] h,int i,string s)=>h.Length>=i+s.Length&&Encoding.ASCII.GetString(h,i,s.Length)==s;
 private static bool IsImage(byte[] h)=>(h.Length>=8&&h.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))||(h.Length>2&&h[0]==255&&h[1]==216&&h[2]==255)||At(h,0,"GIF87a")||At(h,0,"GIF89a")||(At(h,0,"RIFF")&&At(h,8,"WEBP"));
}
