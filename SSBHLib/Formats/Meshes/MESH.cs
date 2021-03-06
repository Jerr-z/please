using SSBHLib.IO;

namespace SSBHLib.Formats.Meshes
{
    public enum SsbVertexAttribFormat
    {
        Float = 0,
        Byte = 2,
        HalfFloat = 5,
        HalfFloat2 = 8,
    }

    [SsbhFile("HSEM")]
    public class Mesh : SsbhFile
    {
        public uint Magic { get; set; } = 0x4D455348;

        public ushort VersionMajor { get; set; } = 0x1;// 0x0001

        public ushort VersionMinor { get; set; } = 0xA;// 0x000A and 0x0008 - Most use 0x000A

        public string ModelName { get; set; } = "";// unused

        public float BoundingSphereX { get; set; }
        public float BoundingSphereY { get; set; }
        public float BoundingSphereZ { get; set; }
        public float BoundingSphereRadius { get; set; }

        public float MinBoundingBoxX { get; set; }
        public float MinBoundingBoxY { get; set; }
        public float MinBoundingBoxZ { get; set; }

        public float MaxBoundingBoxX { get; set; }
        public float MaxBoundingBoxY { get; set; }
        public float MaxBoundingBoxZ { get; set; }
        
        public float ObbCenterX { get; set; }
        public float ObbCenterY { get; set; }
        public float ObbCenterZ { get; set; }

        public float M11 { get; set; }
        public float M12 { get; set; }
        public float M13 { get; set; }
        public float M21 { get; set; }
        public float M22 { get; set; }
        public float M23 { get; set; }
        public float M31 { get; set; }
        public float M32 { get; set; }
        public float M33 { get; set; }

        public float ObbSizeX { get; set; }
        public float ObbSizeY { get; set; }
        public float ObbSizeZ { get; set; }

        public float UnkBounding0 { get; set; }
        
        public MeshObject[] Objects { get; set; }
        
        public int[] BufferSizes { get; set; }
        
        public long PolygonIndexSize { get; set; } // seems to match index count?
        
        public MeshBuffer[] VertexBuffers { get; set; }
        
        public byte[] PolygonBuffer { get; set; }
        
        public MeshRiggingGroup[] RiggingBuffers { get; set; }
        
        public long UnknownOffset { get; set; }
        
        public long UnknownSize { get; set; }
    }
}
