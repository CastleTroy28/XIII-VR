using System;
using System.IO;
using System.IO.Compression;
using System.Text;
namespace XiiiXR;
// Managed-only worker code. No Unity object, texture or native pointer crosses
// onto the PNG compression / file-write thread.
internal static class CapturePng
{
    internal static void Write(string path,int width,int height,byte[] bottomUpRgb)
    {
        if(width<=0||height<=0||bottomUpRgb.Length!=checked(width*height*3))throw new ArgumentException("Invalid capture bytes");
        using var file=File.Create(path);file.Write(new byte[]{137,80,78,71,13,10,26,10});
        var header=new byte[13];Number(header,0,(uint)width);Number(header,4,(uint)height);header[8]=8;header[9]=2;Chunk(file,"IHDR",header);
        using var compressed=new MemoryStream();
        using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))
            for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(bottomUpRgb,y*width*3,width*3);}
        Chunk(file,"IDAT",compressed.ToArray());Chunk(file,"IEND",Array.Empty<byte>());
    }
    private static void Number(byte[] data,int start,uint value){for(int i=0;i<4;i++)data[start+i]=(byte)(value>>(24-8*i));}
    private static void Chunk(Stream stream,string name,byte[] data)
    {
        var length=new byte[4];Number(length,0,(uint)data.Length);stream.Write(length);
        var type=Encoding.ASCII.GetBytes(name);stream.Write(type);stream.Write(data);
        uint crc=0xffffffff;void Add(byte b){crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
        foreach(byte b in type)Add(b);foreach(byte b in data)Add(b);Number(length,0,~crc);stream.Write(length);
    }
}
