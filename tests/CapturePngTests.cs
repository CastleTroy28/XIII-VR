using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Buffers.Binary;using XiiiXR;
class CapturePngTests
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main()
 {
  var path="xiii-xr/build/capture-test.png";
  // Bottom row: red, green. Top row: blue, yellow. Asymmetric pattern catches
  // vertical and horizontal reversals as well as accidental eye-order changes.
  CapturePng.Write(path,2,2,new byte[]{255,0,0,0,255,0,0,0,255,255,255,0});
  var bytes=File.ReadAllBytes(path);Check(bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"PNG signature");
  byte[]? compressed=null;int chunks=0;
  for(int i=8;i<bytes.Length;)
  {
   int n=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i,4));string type=System.Text.Encoding.ASCII.GetString(bytes,i+4,4);
   if(type=="IHDR")Check(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i+8,4))==2&&bytes[i+16]==8&&bytes[i+17]==2,"not RGB24");
   if(type=="IDAT")compressed=bytes.AsSpan(i+8,n).ToArray();
   uint crc=uint.MaxValue;foreach(var b in bytes.AsSpan(i+4,n+4)){crc^=b;for(int j=0;j<8;j++)crc=(crc>>1)^(0xedb88320u&unchecked((uint)-(int)(crc&1)));}
   Check(~crc==BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i+8+n,4)),"bad PNG CRC");i+=n+12;chunks++;
  }
  using var input=new MemoryStream(compressed!);using var z=new ZLibStream(input,CompressionMode.Decompress);using var raw=new MemoryStream();z.CopyTo(raw);
  Check(raw.ToArray().SequenceEqual(new byte[]{0,0,0,255,255,255,0,0,255,0,0,0,255,0}),"pixel orientation or row filter changed");Check(chunks==3,"incomplete PNG");
  bool rejected=false;try{CapturePng.Write(path+".invalid",2,2,new byte[1]);}catch(ArgumentException){rejected=true;}Check(rejected&&!File.Exists(path+".invalid"),"malformed capture written");
  Console.WriteLine("PASS: managed PNG signature, chunks, CRC, zlib roundtrip, asymmetric pixel orientation, malformed-input rejection. Unity GPU readback not simulated.");
 }
}
