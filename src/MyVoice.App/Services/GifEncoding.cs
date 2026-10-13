using System;
using System.IO;
using System.Windows.Media.Imaging;
namespace MyVoice.App.Services;
public static class GifEncoding
{
    // WPF's GIF encoder drops frame delays supplied as BitmapMetadata.
    // Apply them to the encoded graphic control blocks, walking actual GIF blocks.
    public static void Save(string path,GifBitmapEncoder encoder,int[] delays)
    {
        using var stream=new MemoryStream();encoder.Save(stream);var bytes=stream.ToArray();
        int offset=13+((bytes[10]&128)!=0?3*(1<<((bytes[10]&7)+1)):0),frame=0;
        void SkipBlocks(){while(offset<bytes.Length){int size=bytes[offset++];if(size==0)return;offset+=size;}throw new InvalidDataException("GIF incomplet.");}
        while(offset<bytes.Length)
        {
            byte kind=bytes[offset++];if(kind==0x3b)break;
            if(kind==0x21)
            {
                byte label=bytes[offset++];
                if(label==0xf9&&bytes[offset]==4)
                {int delay=Math.Clamp(delays[Math.Min(frame,delays.Length-1)]/10,2,1000);bytes[offset+2]=(byte)delay;bytes[offset+3]=(byte)(delay>>8);}
                SkipBlocks();
            }
            else if(kind==0x2c)
            {
                int packed=bytes[offset+8];offset+=9;if((packed&128)!=0)offset+=3*(1<<((packed&7)+1));offset++;SkipBlocks();frame++;
            }
            else throw new InvalidDataException("Bloc GIF invalide.");
        }
        if(frame!=delays.Length)throw new InvalidDataException("Nombre d’images GIF incohérent.");
        File.WriteAllBytes(path,bytes);
    }
}
