using System;
using System.Collections.Generic;
using System.IO;
using SSBHLib.Formats.Meshes;
using System.Linq;

namespace SSBHLib.Tools
{
    /// <summary>
    /// Helps generate a MESH file
    /// </summary>
    public class SsbhMeshMaker
    {
        private class TempMesh
        {
            public string Name;
            public string ParentBone;
            public int VertexCount;
            public SsbhVertexAttribute BoundingSphere;
            public SsbhVertexAttribute BbMin;
            public SsbhVertexAttribute BbMax;
            public SsbhVertexAttribute ObbCenter;
            public SsbhVertexAttribute ObbSize;
            public float[] ObbMatrix3X3;
            public Dictionary<UltimateVertexAttribute, float[]> VertexData = new Dictionary<UltimateVertexAttribute, float[]>();
            public List<uint> Indices = new List<uint>();
            public List<SsbhVertexInfluence> Influences = new List<SsbhVertexInfluence>();
        }

        private TempMesh currentMesh;
        private List<TempMesh> meshes = new List<TempMesh>();

        /// <summary>
        /// Begins creating a new mesh object with given attributes
        /// </summary>
        /// <param name="name">The name of the Mesh Object</param>
        /// <param name="indices">The vertex indices as triangles</param>
        /// <paramref name="positions"/>
        /// <param name="parentBoneName"></param>
        /// <param name="generateBounding"></param>
        public void StartMeshObject(string name, uint[] indices, SsbhVertexAttribute[] positions, string parentBoneName = "", bool generateBounding = false)
        {
            currentMesh = new TempMesh
            {
                Name = name,
                ParentBone = parentBoneName
            };
            currentMesh.Indices.AddRange(indices);
            currentMesh.VertexCount = positions.Length;

            meshes.Add(currentMesh);
            AddAttributeToMeshObject(UltimateVertexAttribute.Position0, positions);

            if (generateBounding)
            {
                //TODO: sphere generation
                BoundingBoxGenerator.GenerateAabb(positions, out SsbhVertexAttribute max, out SsbhVertexAttribute min);
                SetAaBoundingBox(min, max);
                SetOrientedBoundingBox(
                    new SsbhVertexAttribute((max.X + min.X / 2), (max.Y + min.Y / 2), (max.Y + min.Y / 2)),
                    new SsbhVertexAttribute((max.X - min.X), (max.Y - min.Y), (max.Z - min.Z)),
                    new float[] {
                        1, 0, 0,
                        0, 1, 0,
                        0, 0, 1});
            }
        }

        /// <summary>
        /// Sets bounding sphere of current mesh
        /// </summary>
        public void SetBoundingSphere(float x, float y, float z, float r)
        {
            if (currentMesh == null)
                return;
            currentMesh.BoundingSphere = new SsbhVertexAttribute()
            {
                X = x,
                Y = y,
                Z = z,
                W = r
            };
        }

        /// <summary>
        /// Sets the axis aligned bounding box for the current Mesh
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void SetAaBoundingBox(SsbhVertexAttribute min, SsbhVertexAttribute max)
        {
            if (currentMesh == null)
                return;
            currentMesh.BbMax = max;
            currentMesh.BbMin = min;
        }

        /// <summary>
        /// Sets the oriented bounding box for the current Mesh
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="matrix3X3"></param>
        public void SetOrientedBoundingBox(SsbhVertexAttribute center, SsbhVertexAttribute size, float[] matrix3X3)
        {
            if (currentMesh == null)
                return;
            if (matrix3X3 == null)
                return;
            if (matrix3X3.Length != 9)
                throw new IndexOutOfRangeException("Matrix must contain 9 entries in row major order");
            currentMesh.ObbCenter = center;
            currentMesh.ObbSize = size;
            currentMesh.ObbMatrix3X3 = matrix3X3;
        }

        /// <summary>
        /// Adds new attribute data to the mesh object
        /// Note: must call StartMeshObject first
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="inputValues"></param>
        public void AddAttributeToMeshObject(UltimateVertexAttribute attribute, SsbhVertexAttribute[] inputValues)
        {
            // TODO: Why must StartMeshObject be called?
            if (currentMesh == null)
                return;

            int size = GetAttributeSize(attribute);
            float[] values = new float[inputValues.Length * size];
            for (int i = 0; i < inputValues.Length; i++)
            {
                if (size > 0)
                    values[i * size + 0] = inputValues[i].X;
                if (size > 1)
                    values[i * size + 1] = inputValues[i].Y;
                if (size > 2)
                    values[i * size + 2] = inputValues[i].Z;
                if (size > 3)
                    values[i * size + 3] = inputValues[i].W;
            }

            currentMesh.VertexData.Add(attribute, values);
        }

        /// <summary>
        /// Attaches rigging information to mesh object
        /// Note: must call StartMeshObject first
        /// </summary>
        public void AttachRiggingToMeshObject(SsbhVertexInfluence[] influences)
        {
            currentMesh?.Influences.AddRange(influences);
        }

        /// <summary>
        /// Creates and returns a mesh file
        /// </summary>
        /// <returns></returns>
        public Mesh GetMeshFile()
        {
            Mesh mesh = new Mesh
            {
                //TODO: bounding box stuff
                // Rigging
                Objects = new MeshObject[meshes.Count]
            };

            // create mesh objects and buffers
            BinaryWriter vertexBuffer1 = new BinaryWriter(new MemoryStream());
            BinaryWriter vertexBuffer2 = new BinaryWriter(new MemoryStream()); // there are actually 4 buffers, but only 2 seem to be used
            BinaryWriter indexBuffer = new BinaryWriter(new MemoryStream());
            int finalBufferOffset = 0;
            int meshIndex = 0;
            Dictionary<string, int> meshGroups = new Dictionary<string, int>();
            List<MeshRiggingGroup> riggingGroups = new List<MeshRiggingGroup>();
            foreach (var tempmesh in meshes)
            {
                MeshObject mo = new MeshObject
                {
                    Name = tempmesh.Name
                };
                if (meshGroups.ContainsKey(mo.Name))
                    meshGroups[mo.Name] += 1;
                else
                    meshGroups.Add(mo.Name, 0);

                mo.SubMeshIndex = meshGroups[mo.Name];
                mo.BoundingSphereX = tempmesh.BoundingSphere.X;
                mo.BoundingSphereY = tempmesh.BoundingSphere.Y;
                mo.BoundingSphereZ = tempmesh.BoundingSphere.Z;
                mo.BoundingSphereRadius = tempmesh.BoundingSphere.W;

                mo.MaxBoundingBoxX = tempmesh.BbMax.X;
                mo.MaxBoundingBoxY = tempmesh.BbMax.Y;
                mo.MaxBoundingBoxZ = tempmesh.BbMax.Z;
                mo.MinBoundingBoxX = tempmesh.BbMin.X;
                mo.MinBoundingBoxY = tempmesh.BbMin.Y;
                mo.MinBoundingBoxZ = tempmesh.BbMin.Z;

                mo.ObbCenterX = tempmesh.ObbCenter.X;
                mo.ObbCenterY = tempmesh.ObbCenter.Y;
                mo.ObbCenterZ = tempmesh.ObbCenter.Z;

                mo.ObbSizeX = tempmesh.ObbSize.X;
                mo.ObbSizeY = tempmesh.ObbSize.Y;
                mo.ObbSizeZ = tempmesh.ObbSize.Z;

                mo.M11 = tempmesh.ObbMatrix3X3[0];
                mo.M12 = tempmesh.ObbMatrix3X3[1];
                mo.M13 = tempmesh.ObbMatrix3X3[2];
                mo.M21 = tempmesh.ObbMatrix3X3[3];
                mo.M22 = tempmesh.ObbMatrix3X3[4];
                mo.M23 = tempmesh.ObbMatrix3X3[5];
                mo.M31 = tempmesh.ObbMatrix3X3[6];
                mo.M32 = tempmesh.ObbMatrix3X3[7];
                mo.M33 = tempmesh.ObbMatrix3X3[8];


                // Create Rigging
                riggingGroups.Add(SsbhRiggingCompiler.CreateRiggingGroup(mo.Name, (int)mo.SubMeshIndex, tempmesh.Influences.ToArray()));

                // set object
                mesh.Objects[meshIndex++] = mo;

                mo.ParentBoneName = tempmesh.ParentBone;
                if (tempmesh.Influences.Count > 0 && (tempmesh.ParentBone == null || tempmesh.ParentBone.Equals("")))
                    mo.HasRigging = 1;

                int stride1 = 0;
                int stride2 = 0;

                mo.VertexOffset = (int)vertexBuffer1.BaseStream.Length;
                mo.VertexOffset2 = (int)vertexBuffer2.BaseStream.Length;
                mo.ElementOffset = (uint)indexBuffer.BaseStream.Length;

                // gather strides
                mo.Attributes = new MeshAttribute[tempmesh.VertexData.Count];
                int attributeIndex = 0;
                foreach (var keypair in tempmesh.VertexData)
                {
                    MeshAttribute attr = new MeshAttribute
                    {
                        Name = GetAttributeName(keypair.Key),
                        Index = GetAttributeIndex(keypair.Key),
                        BufferIndex = GetBufferIndex(keypair.Key),
                        DataType = GetAttributeDataType(keypair.Key),
                        BufferOffset = (GetBufferIndex(keypair.Key) == 0) ? stride1 : stride2,
                        AttributeStrings = new MeshAttributeString[] { new MeshAttributeString() { Name = keypair.Key.ToString() } }
                    };
                    mo.Attributes[attributeIndex++] = attr;

                    if (GetBufferIndex(keypair.Key) == 0)
                        stride1 += GetAttributeSize(keypair.Key) * GetAttributeDataSize(keypair.Key);
                    else
                        stride2 += GetAttributeSize(keypair.Key) * GetAttributeDataSize(keypair.Key);
                }

                // now that strides are known...
                long buffer1Start = vertexBuffer1.BaseStream.Length;
                long buffer2Start = vertexBuffer2.BaseStream.Length;
                vertexBuffer1.Write(new byte[stride1 * tempmesh.VertexCount]);
                vertexBuffer2.Write(new byte[stride2 * tempmesh.VertexCount]);
                attributeIndex = 0;
                foreach (var keypair in tempmesh.VertexData)
                {
                    var attr = mo.Attributes[attributeIndex++];
                    float[] data = keypair.Value;
                    var buffer = attr.BufferIndex == 0 ? vertexBuffer1 : vertexBuffer2;
                    int bufferOffset = (int)(attr.BufferIndex == 0 ? buffer1Start : buffer2Start);
                    int stride = (attr.BufferIndex == 0 ? stride1 : stride2);
                    int size = GetAttributeSize(keypair.Key);
                    for (int vertexIndex = 0; vertexIndex < tempmesh.VertexCount; vertexIndex++)
                    {
                        buffer.Seek(bufferOffset + stride * vertexIndex + attr.BufferOffset, SeekOrigin.Begin);
                        for (int j = 0; j < size; j++)
                        {
                            WriteType(buffer, attr.DataType, data[vertexIndex * size + j]);
                        }
                    }
                    // seek to end just to make sure
                    buffer.Seek((int)buffer.BaseStream.Length, SeekOrigin.Begin);
                }

                mo.FinalBufferOffset = finalBufferOffset;
                finalBufferOffset += (4 + stride1) * tempmesh.VertexCount;
                mo.VertexCount = tempmesh.VertexCount;
                mo.IndexCount = tempmesh.Indices.Count;
                mo.Stride = stride1;
                mo.Stride2 = stride2;

                // write index buffer
                if (tempmesh.VertexCount > ushort.MaxValue)
                {
                    mo.DrawElementType = 1;
                    foreach (var i in tempmesh.Indices)
                        indexBuffer.Write(i);
                }
                else
                {
                    foreach (var i in tempmesh.Indices)
                        indexBuffer.Write((ushort)i);
                }
            }

            mesh.PolygonIndexSize = indexBuffer.BaseStream.Length;
            mesh.BufferSizes = new int[] { (int)vertexBuffer1.BaseStream.Length, (int)vertexBuffer2.BaseStream.Length, 0, 0 };
            mesh.PolygonBuffer = ((MemoryStream)indexBuffer.BaseStream).ToArray();
            Console.WriteLine(mesh.PolygonBuffer.Length + " " + indexBuffer.BaseStream.Length);
            mesh.VertexBuffers = new MeshBuffer[]
            {
                new MeshBuffer { Buffer = ((MemoryStream)vertexBuffer1.BaseStream).ToArray() },
                new MeshBuffer { Buffer = ((MemoryStream)vertexBuffer2.BaseStream).ToArray() },
                new MeshBuffer { Buffer = new byte[0] },
                new MeshBuffer { Buffer = new byte[0] }
            };

            mesh.RiggingBuffers = riggingGroups.ToArray().OrderBy(o => o.Name, StringComparer.Ordinal).ToArray();

            vertexBuffer1.Close();
            vertexBuffer2.Close();
            indexBuffer.Close();

            return mesh;
        }

        private void WriteType(BinaryWriter writer, int type, float value)
        {
            switch (type)
            {
                case 0:
                    writer.Write(value);
                    break;
                case 2:
                    writer.Write((byte)value);
                    break;
                case 5:
                    writer.Write((ushort)FromFloat(value));
                    break;
                case 8:
                    writer.Write((ushort)FromFloat(value));
                    break;
            }
        }

        private int GetBufferIndex(UltimateVertexAttribute attribute)
        {
            switch (attribute)
            {
                case UltimateVertexAttribute.Position0:
                case UltimateVertexAttribute.Normal0:
                case UltimateVertexAttribute.Tangent0:
                    return 0;
                default:
                    return 1;
            }
        }

        public static int GetAttributeSize(UltimateVertexAttribute attribute)
        {
            switch (attribute)
            {
                case UltimateVertexAttribute.Position0:
                    return 3;
                case UltimateVertexAttribute.Normal0:
                    return 4;
                case UltimateVertexAttribute.Tangent0:
                    return 4;
                case UltimateVertexAttribute.Map1:
                case UltimateVertexAttribute.UvSet:
                case UltimateVertexAttribute.UvSet1:
                case UltimateVertexAttribute.UvSet2:
                case UltimateVertexAttribute.Bake1:
                    return 2;
                case UltimateVertexAttribute.ColorSet1:
                case UltimateVertexAttribute.ColorSet2:
                case UltimateVertexAttribute.ColorSet21:
                case UltimateVertexAttribute.ColorSet22:
                case UltimateVertexAttribute.ColorSet23:
                case UltimateVertexAttribute.ColorSet3:
                case UltimateVertexAttribute.ColorSet4:
                case UltimateVertexAttribute.ColorSet5:
                case UltimateVertexAttribute.ColorSet6:
                case UltimateVertexAttribute.ColorSet7:
                    return 4;
                default:
                    return 3;
            }
        }

        private static string GetAttributeName(UltimateVertexAttribute attribute)
        {
            switch (attribute)
            {
                case UltimateVertexAttribute.Tangent0:
                    return "map1";
                default:
                    return attribute.ToString();
            }
        }

        private static int GetAttributeDataSize(UltimateVertexAttribute attribute)
        {
            // TODO: Use enum?
            switch (GetAttributeDataType(attribute))
            {
                case 0:
                    return 4;
                case 2:
                    return 1;
                case 5:
                    return 2;
                case 8:
                    return 2;
                default:
                    return 1;
            }
        }

        private static int GetAttributeDataType(UltimateVertexAttribute attribute)
        {
            // TODO: Use enum?
            switch (attribute)
            {
                case UltimateVertexAttribute.Position0:
                    return 0;
                case UltimateVertexAttribute.Normal0:
                case UltimateVertexAttribute.Tangent0:
                    return 5;
                case UltimateVertexAttribute.Map1:
                case UltimateVertexAttribute.UvSet:
                case UltimateVertexAttribute.UvSet1:
                case UltimateVertexAttribute.UvSet2:
                case UltimateVertexAttribute.Bake1:
                    return 8;
                case UltimateVertexAttribute.ColorSet1:
                case UltimateVertexAttribute.ColorSet2:
                case UltimateVertexAttribute.ColorSet21:
                case UltimateVertexAttribute.ColorSet22:
                case UltimateVertexAttribute.ColorSet23:
                case UltimateVertexAttribute.ColorSet3:
                case UltimateVertexAttribute.ColorSet4:
                case UltimateVertexAttribute.ColorSet5:
                case UltimateVertexAttribute.ColorSet6:
                case UltimateVertexAttribute.ColorSet7:
                    return 2;
                default:
                    return -1;
            }
        }

        private static int GetAttributeIndex(UltimateVertexAttribute attribute)
        {
            switch (attribute)
            {
                case UltimateVertexAttribute.Position0:
                    return 0;
                case UltimateVertexAttribute.Normal0:
                    return 1;
                case UltimateVertexAttribute.Tangent0:
                    return 3;
                case UltimateVertexAttribute.Map1:
                case UltimateVertexAttribute.UvSet:
                case UltimateVertexAttribute.UvSet1:
                case UltimateVertexAttribute.UvSet2:
                case UltimateVertexAttribute.Bake1:
                    return 4;
                case UltimateVertexAttribute.ColorSet1:
                case UltimateVertexAttribute.ColorSet2:
                case UltimateVertexAttribute.ColorSet21:
                case UltimateVertexAttribute.ColorSet22:
                case UltimateVertexAttribute.ColorSet23:
                case UltimateVertexAttribute.ColorSet3:
                case UltimateVertexAttribute.ColorSet4:
                case UltimateVertexAttribute.ColorSet5:
                case UltimateVertexAttribute.ColorSet6:
                case UltimateVertexAttribute.ColorSet7:
                    return 5;
                default:
                    return -1;
            }
        }

        private static int SingleToInt32Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        private static int FromFloat(float floatValue)
        {
            int floatBits = SingleToInt32Bits(floatValue);
            int sign = floatBits >> 16 & 0x8000;          // sign only
            int val = (floatBits & 0x7fffffff) + 0x1000; // rounded value

            if (val >= 0x47800000)               // might be or become NaN/Inf
            {                                     // avoid Inf due to rounding
                if ((floatBits & 0x7fffffff) >= 0x47800000)
                {                                 // is or must become NaN/Inf
                    if (val < 0x7f800000)        // was value but too large
                        return sign | 0x7c00;     // make it +/-Inf
                    return sign | 0x7c00 |        // remains +/-Inf or NaN
                        (floatBits & 0x007fffff) >> 13; // keep NaN (and Inf) bits
                }
                return sign | 0x7bff;             // unrounded not quite Inf
            }
            if (val >= 0x38800000)               // remains normalized value
                return sign | val - 0x38000000 >> 13; // exp - 127 + 15
            if (val < 0x33000000)                // too small for subnormal
                return sign;                      // becomes +/-0
            val = (floatBits & 0x7fffffff) >> 23;  // tmp exp for subnormal calc
            return sign | ((floatBits & 0x7fffff | 0x800000) // add subnormal bit
                + (0x800000 >> val - 102)     // round depending on cut off
                >> 126 - val);   // div by 2^(1-(exp-127+15)) and >> 13 | exp=0
        }

    }
}
