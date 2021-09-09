﻿using System;
using SSBHLib;
using SSBHLib.Formats.Materials;
using System.Xml.Serialization;
using System.IO;

namespace MatLab
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || args[0] == "--help" || args[0] == "-h") {
                Console.WriteLine("Usage:\nmatlab [input file] ([output file])");
                return;
            }

            if (args.Length > 1) {
                ConvertFiles(args[0], args[1]);
            } else {
                ConvertFiles(args[0]);
            }
        }

        private static void ConvertFiles(string inputPath, string outputPath = null)
        {

            XmlSerializer serializer = new XmlSerializer(typeof(MaterialLibrary));
            switch (Path.GetExtension(inputPath))
            {
                case ".numatb":
                    SerializeMatl(inputPath, outputPath, serializer);
                    break;
                case ".xml":
                    DeserializeXml(inputPath, outputPath, serializer);
                    break;
            }
        }

        private static void DeserializeXml(string inputPath, string outputPath, XmlSerializer serializer)
        {
            if (outputPath == null)
                outputPath = GetFullPathWithoutExtension(inputPath) + "_out.numatb";
            Console.WriteLine($"Converting {Path.GetFileName(inputPath)} to {outputPath}...");
            using (TextReader reader = new StringReader(File.ReadAllText(inputPath)))
            {
                var result = (MaterialLibrary)serializer.Deserialize(reader);

                Matl newmatl = LibraryToMATL(result);

                Ssbh.TrySaveSsbhFile(outputPath, newmatl);
            }
        }

        private static void SerializeMatl(string inputPath, string outputPath, XmlSerializer serializer)
        {
            if (outputPath == null)
                outputPath = GetFullPathWithoutExtension(inputPath) + "_out.xml";

            Console.WriteLine($"Converting {Path.GetFileName(inputPath)} to {outputPath}...");
            if (Ssbh.TryParseSsbhFile(inputPath, out SsbhFile file))
            {
                Matl matlFile = (Matl)file;

                MaterialLibrary library = MATLtoLibrary(matlFile);

                using (TextWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, library);
                    string serial = writer.ToString();
                    File.WriteAllText(outputPath, serial);
                }
            }
            else
            {
                Console.WriteLine("Error reading matl file");
            }
        }

        public static string GetFullPathWithoutExtension(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        }

        public static Matl LibraryToMATL(MaterialLibrary library)
        {
            Matl matl = new Matl
            {
                Entries = new MatlEntry[library.material.Length]
            };

            for (int i = 0; i < library.material.Length; i++)
            {
                MatlEntry entry = new MatlEntry
                {
                    MaterialLabel = library.material[i].label,
                    MaterialName = library.material[i].name,
                    Attributes = new MatlAttribute[library.material[i].param.Length]
                };

                for (int j = 0; j < library.material[i].param.Length; j++)
                {
                    entry.Attributes[j] = new MatlAttribute
                    {
                        ParamId = library.material[i].param[j].name,

                        DataObject = library.material[i].param[j].value
                    };
                }

                matl.Entries[i] = entry;
            }

            return matl;
        }

        public static MaterialLibrary MATLtoLibrary(Matl matlFile)
        {
            MaterialLibrary library = new MaterialLibrary
            {
                material = new Material[matlFile.Entries.Length]
            };

            int entryIndex = 0;
            foreach (var entry in matlFile.Entries)
            {
                Material mat = new Material();
                mat.name = entry.MaterialName;
                mat.label = entry.MaterialLabel;

                mat.param = new MatlXmlAttribute[entry.Attributes.Length];

                int attribIndex = 0;
                foreach (var attr in entry.Attributes)
                {
                    MatlXmlAttribute attrib = new MatlXmlAttribute();
                    attrib.name = attr.ParamId;
                    attrib.value = attr.DataObject;
                    mat.param[attribIndex++] = attrib;
                }

                library.material[entryIndex] = mat;
                entryIndex++;
            }

            return library;
        }
    }
}
