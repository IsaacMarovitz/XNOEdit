using System.Buffers.Binary;
using System.IO.Compression;
using Plume;
using Solaris;
using XNOEdit.Logging;

namespace XNOEdit.Guest
{
    public readonly record struct GuestShaderCacheEntry(
        ulong Hash,
        uint DxilOffset,
        uint DxilSize,
        uint SpirvOffset,
        uint SpirvSize,
        uint AirOffset,
        uint AirSize,
        uint SpecConstantsMask,
        string Filename);

    public sealed class GuestShaderCache : IDisposable
    {
        private const uint TableMagic = 0x58534347; // "GCSX"

        private readonly Dictionary<ulong, GuestShaderCacheEntry> _entries;
        private readonly RenderShaderFormat _format;

        private byte[] _blob;

        private GuestShaderCache(
            Dictionary<ulong, GuestShaderCacheEntry> entries, byte[] blob, RenderShaderFormat format)
        {
            _entries = entries;
            _blob = blob;
            _format = format;
        }

        /// <summary>
        /// Loads the table and the blob matching the device's shader format. Safe to
        /// call from a worker thread; nothing here touches the device beyond reading
        /// its reported format.
        /// </summary>
        public static GuestShaderCache Load(SlDevice device, string cacheDirectory)
        {
            ArgumentNullException.ThrowIfNull(device);

            var tablePath = Path.Combine(cacheDirectory, "shader_cache.bin");

            if (!File.Exists(tablePath))
            {
                throw new FileNotFoundException(
                    $"Guest shader cache table not found at '{tablePath}'. " +
                    "Run extract_shader_cache.py against the recomp's shader_cache.cpp.", tablePath);
            }

            var entries = ReadTable(tablePath);
            var format = device.Capabilities.ShaderFormat;

            var blobName = format switch
            {
                RenderShaderFormat.Metal => "shader_cache_air.br",
                RenderShaderFormat.Spirv => "shader_cache_spirv.br",
                RenderShaderFormat.Dxil => "shader_cache_dxil.br",
                _ => throw new PlatformNotSupportedException("The device reported no usable shader format."),
            };

            var blobPath = Path.Combine(cacheDirectory, blobName);

            if (!File.Exists(blobPath))
            {
                throw new FileNotFoundException(
                    $"No {format} bytecode in the guest shader cache ('{blobName}' is missing). " +
                    "The recomp build that produced this cache did not target this backend.", blobPath);
            }

            var blob = Decompress(blobPath);

            Logger.Info?.PrintMsg(LogClass.Application,
                $"Guest shader cache: {entries.Count} shaders, {blob.Length / (1024 * 1024)} MiB of {format} bytecode");

            return new GuestShaderCache(entries, blob, format);
        }

        public bool TryGet(ulong hash, out GuestShaderCacheEntry entry) => _entries.TryGetValue(hash, out entry);

        public ReadOnlySpan<byte> GetBytecode(in GuestShaderCacheEntry entry)
        {
            ObjectDisposedException.ThrowIf(_blob.Length == 0, this);

            var (offset, size) = _format switch
            {
                RenderShaderFormat.Metal => (entry.AirOffset, entry.AirSize),
                RenderShaderFormat.Spirv => (entry.SpirvOffset, entry.SpirvSize),
                RenderShaderFormat.Dxil => (entry.DxilOffset, entry.DxilSize),
                _ => (0u, 0u),
            };

            if (size == 0)
                return ReadOnlySpan<byte>.Empty;

            if ((long)offset + size > _blob.Length)
            {
                throw new InvalidDataException(
                    $"Entry {entry.Hash:X16} ('{entry.Filename}') spans {offset}..{offset + size} " +
                    $"of a {_blob.Length} byte blob. The table and blob are mismatched.");
            }

            var stored = _blob.AsSpan((int)offset, (int)size);

            if (_format == RenderShaderFormat.Spirv)
            {
                throw new NotImplementedException("smolv decoding not implemented.");
            }

            return stored;
        }

        private static byte[] Decompress(string path)
        {
            var file = File.ReadAllBytes(path);

            if (file.Length <= sizeof(ulong))
                throw new InvalidDataException($"'{Path.GetFileName(path)}' is truncated.");

            var decompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(file);

            if (decompressedSize == 0 || decompressedSize > int.MaxValue)
            {
                throw new InvalidDataException(
                    $"'{Path.GetFileName(path)}' declares an implausible size of {decompressedSize} bytes.");
            }

            var blob = GC.AllocateUninitializedArray<byte>((int)decompressedSize);

            if (!BrotliDecoder.TryDecompress(file.AsSpan(sizeof(ulong)), blob, out var written) ||
                written != blob.Length)
            {
                throw new InvalidDataException(
                    $"'{Path.GetFileName(path)}' decompressed to {written} bytes, expected {blob.Length}.");
            }

            return blob;
        }

        private static Dictionary<ulong, GuestShaderCacheEntry> ReadTable(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != TableMagic)
                throw new InvalidDataException($"'{path}' is not a guest shader cache table.");

            var count = reader.ReadInt32();

            if (count < 0)
                throw new InvalidDataException($"'{path}' declares {count} entries.");

            var entries = new Dictionary<ulong, GuestShaderCacheEntry>(count);

            for (var i = 0; i < count; i++)
            {
                var entry = new GuestShaderCacheEntry(
                    reader.ReadUInt64(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadString());

                // Duplicate hashes mean the same shader appears under several paths,
                // which is common — the first filename wins and the bytecode is identical.
                entries.TryAdd(entry.Hash, entry);
            }

            return entries;
        }

        public void Dispose()
        {
            _entries.Clear();
            _blob = [];
        }
    }
}
