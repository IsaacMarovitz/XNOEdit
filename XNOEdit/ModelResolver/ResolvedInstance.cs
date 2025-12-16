using System.Numerics;

namespace XNOEdit.ModelResolver
{
    public struct ResolvedInstance
    {
        public string? ArchiveHint { get; init; }
        public string ModelPath { get; init; }
        public Vector3 Position { get; init; }
        public Quaternion Rotation { get; init; }
        public bool Visible { get; init; }

        public static ResolvedInstance Create(string modelPath, Vector3 position, Quaternion rotation)
        {
            return new ResolvedInstance
            {
                ModelPath = modelPath,
                Position = position,
                Rotation = rotation,
                Visible = true
            };
        }

        public static ResolvedInstance Create(string archiveHint, string modelPath, Vector3 position, Quaternion rotation)
        {
            return new ResolvedInstance
            {
                ArchiveHint = archiveHint,
                ModelPath = modelPath,
                Position = position,
                Rotation = rotation,
                Visible = true
            };
        }
    }
}
