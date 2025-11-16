using System.IO.Compression;
using System.Text;

namespace XNOEdit.Shaders
{
    // TODO: Replace with Marathon U8Archive when it's done
    public class ShaderArchive
    {
        private const uint Signature = 0x55AA382D;
        private const uint TypeMask = 0xFF000000;
        private const uint NameOffsetMask = 0x00FFFFFF;

        private const uint SHADER_MAGIC = 0x102A1100;
        private const uint SHADER_MAGIC_MASK = 0xFFFFFF00;

        private FileStream _archiveStream;
        private List<Entry> _entries = [];
        private DirectoryNode _root;

        public DirectoryNode Root => _root;


        public ShaderArchive(string arcPath)
        {
            _archiveStream = File.OpenRead(arcPath);
            using var reader = new BinaryReader(_archiveStream,  Encoding.UTF8, true);

            var signature = ReadBeUint32(reader);
            if (signature != Signature)
            {
                throw new Exception($"Invalid signature: {signature}");
            }

            var entriesOffset = ReadBeUint32(reader);
            var entriesLength = ReadBeUint32(reader);
            var dataOffset = ReadBeUint32(reader);

            _archiveStream.Seek(16, SeekOrigin.Current);
            _archiveStream.Seek(entriesOffset, SeekOrigin.Begin);

            var root = new Entry
            {
                Flags = ReadBeUint32(reader),
                Offset = ReadBeUint32(reader),
                Length = ReadBeUint32(reader),
                UncompressedSize = ReadBeUint32(reader)
            };

            _entries.Add(root);

            for (var i = 1; i < root.Length; i++)
            {
                var entry = new Entry
                {
                    Flags = ReadBeUint32(reader),
                    Offset = ReadBeUint32(reader),
                    Length = ReadBeUint32(reader),
                    UncompressedSize = ReadBeUint32(reader)
                };

                _entries.Add(entry);
            }

            var stringTableOffset = entriesOffset + (root.Length * 16);

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                _archiveStream.Seek(stringTableOffset + entry.GetNameOffset(), SeekOrigin.Begin);
                entry.Name = ReadNullTerminatedString(_archiveStream);

                _entries[i] = entry;
            }

            _root = new DirectoryNode("");
            BuildTree(0, _root);
        }

        private int BuildTree(int entryIndex, DirectoryNode parentDir)
        {
            var entry = _entries[entryIndex];

            if (entry.GetEntryType() == EntryType.Directory)
            {
                var dir = new DirectoryNode(entry.Name);
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    parentDir.AddChild(dir);
                }

                // Process children
                var childIndex = entryIndex + 1;
                while (childIndex < entry.Length)
                {
                    childIndex = BuildTree(childIndex, string.IsNullOrEmpty(entry.Name) ? parentDir : dir);
                }

                return (int)entry.Length;
            }
            else
            {
                var file = new FileNode(entry.Name, entryIndex, this);
                parentDir.AddChild(file);
                return entryIndex + 1;
            }
        }

        private byte[] ExtractFile(int entryIndex)
        {
            var entry = _entries[entryIndex];

            if (entry.GetEntryType() == EntryType.Directory)
            {
                throw new InvalidOperationException("Cannot extract a directory");
            }

            lock (_archiveStream)
            {
                _archiveStream.Seek(entry.Offset, SeekOrigin.Begin);

                var data = new byte[entry.Length];
                _archiveStream.ReadExactly(data, 0, (int)entry.Length);

                if (entry.IsCompressed())
                {
                    using var compressedStream = new MemoryStream(data);
                    using var decompressedStream = new MemoryStream((int)entry.UncompressedSize);
                    using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
                    zlibStream.CopyTo(decompressedStream);
                    return decompressedStream.ToArray();
                }

                return data;
            }
        }

        public FileNode GetFile(string path)
        {
            var parts = path.Split('/', '\\').Where(p => !string.IsNullOrEmpty(p)).ToArray();
            Node current = _root;

            foreach (var part in parts)
            {
                if (current is DirectoryNode dir)
                {
                    current = dir.GetChild(part);
                    if (current == null)
                    {
                        throw new FileNotFoundException($"Path not found: {path}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Path traversal error: {path}");
                }
            }

            if (current is FileNode file)
            {
                return file;
            }

            throw new FileNotFoundException($"Not a file: {path}");
        }

        public DirectoryNode GetDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/" || path == "\\")
            {
                return _root;
            }

            var parts = path.Split('/', '\\').Where(p => !string.IsNullOrEmpty(p)).ToArray();
            Node current = _root;

            foreach (var part in parts)
            {
                if (current is DirectoryNode dir)
                {
                    current = dir.GetChild(part);
                    if (current == null)
                    {
                        throw new DirectoryNotFoundException($"Path not found: {path}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Not a directory: {path}");
                }
            }

            if (current is DirectoryNode resultDir)
            {
                return resultDir;
            }

            throw new DirectoryNotFoundException($"Not a directory: {path}");
        }

        private static string ReadNullTerminatedString(FileStream stream)
        {
            List<byte> bytes = [];
            int currentByte;

            while ((currentByte = stream.ReadByte()) != -1 && currentByte != 0)
            {
                bytes.Add((byte)currentByte);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static void DecompressZlib(Stream source, Stream destination)
        {
            var stream = new ZLibStream(source, CompressionMode.Decompress);
            stream.CopyTo(destination);
        }

        private static uint ReadBeUint32(BinaryReader source)
        {
            var little = source.ReadBytes(4);
            Array.Reverse(little);
            return BitConverter.ToUInt32(little, 0);
        }

        private enum EntryType : byte
        {
            File = 0,
            Directory = 1
        }

        private struct Entry
        {
            public uint Flags;
            public uint Offset;
            public uint Length;
            public uint UncompressedSize;
            public string Name;

            public EntryType GetEntryType()
            {
                return (EntryType)((Flags & TypeMask) >> 24);
            }

            public uint GetNameOffset()
            {
                return Flags & NameOffsetMask;
            }

            public bool IsCompressed()
            {
                return Length != 0 && UncompressedSize != 0;
            }
        }

         public abstract class Node
        {
            public string Name { get; }

            protected Node(string name)
            {
                Name = name;
            }
        }

        public class DirectoryNode : Node
        {
            private Dictionary<string, Node> children = new(StringComparer.OrdinalIgnoreCase);

            public IReadOnlyCollection<Node> Children => children.Values;

            public DirectoryNode(string name) : base(name) { }

            public void AddChild(Node node)
            {
                children[node.Name] = node;
            }

            public Node GetChild(string name)
            {
                children.TryGetValue(name, out var child);
                return child;
            }

            public IEnumerable<FileNode> GetAllFiles()
            {
                foreach (var child in children.Values)
                {
                    if (child is FileNode file)
                    {
                        yield return file;
                    }
                    else if (child is DirectoryNode dir)
                    {
                        foreach (var subFile in dir.GetAllFiles())
                        {
                            yield return subFile;
                        }
                    }
                }
            }
        }

        public class FileNode : Node
        {
            private readonly int entryIndex;
            private readonly ShaderArchive archive;
            private byte[] cachedData;

            public FileNode(string name, int entryIndex, ShaderArchive archive) : base(name)
            {
                this.entryIndex = entryIndex;
                this.archive = archive;
            }

            public byte[] GetData(bool useCache = true)
            {
                if (useCache && cachedData != null)
                {
                    return cachedData;
                }

                var data = archive.ExtractFile(entryIndex);

                if (useCache)
                {
                    cachedData = data;
                }

                return data;
            }
        }

        private static uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) |
                          (data[offset + 1] << 16) |
                          (data[offset + 2] << 8) |
                          data[offset + 3]);
        }

        public static List<byte[]> ExtractShaderContainers(byte[] fileData)
        {
            var shaders = new List<byte[]>();
            const int minHeaderSize = 0x24;

            for (var i = 0; i <= fileData.Length - minHeaderSize; )
            {
                // Read flags as BIG-ENDIAN
                var flags = ReadBigEndianUInt32(fileData, i);

                // Check magic number
                if ((flags & SHADER_MAGIC_MASK) == SHADER_MAGIC)
                {
                    // Read virtualSize and physicalSize as BIG-ENDIAN
                    var virtualSize = ReadBigEndianUInt32(fileData, i + 4);
                    var physicalSize = ReadBigEndianUInt32(fileData, i + 8);

                    var dataSize = (int)(virtualSize + physicalSize);

                    if (dataSize > 0 && dataSize <= (fileData.Length - i) && dataSize < 10_000_000)
                    {
                        // Validate field1C and field20 are zero
                        var field1C = ReadBigEndianUInt32(fileData, i + 0x1C);
                        var field20 = ReadBigEndianUInt32(fileData, i + 0x20);

                        if (field1C == 0 && field20 == 0)
                        {
                            // Extract the entire shader container (keep in big-endian format)
                            var shaderData = new byte[dataSize];
                            Array.Copy(fileData, i, shaderData, 0, dataSize);
                            shaders.Add(shaderData);

                            Console.WriteLine($"Found shader container at offset 0x{i:X}, size: {dataSize} bytes");

                            i += dataSize;
                            continue;
                        }
                    }
                }

                i += 4;
            }

            return shaders;
        }
    }
}
