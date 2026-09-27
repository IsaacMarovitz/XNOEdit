using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using Marathon.Formats.Ninja.Types;

namespace XNOEdit.Render
{
    public struct NodeTransform
    {
        public Vector3 Translation;
        public Vector3 Rotation;
        public Vector3 Scale;
    }

    public sealed class Skeleton
    {
        /// <summary>Ninja angles are fixed point with 0x10000 per turn.</summary>
        public const float AngleToRadians = MathF.Tau / 65536.0f;

        private readonly List<Node> _nodes;
        private readonly NodeTransform[] _bind;
        private readonly int[] _slotNodes;
        private readonly Matrix4x4[] _inverseBind;
        private readonly Matrix4x4[] _attachment;
        private readonly Matrix4x4[] _world;

        public Skeleton(ObjectChunk objectChunk)
        {
            _nodes = objectChunk.Nodes;
            _bind = new NodeTransform[_nodes.Count];
            _slotNodes = new int[objectChunk.MatrixIndexCount];
            _inverseBind = new Matrix4x4[_nodes.Count];
            _attachment = new Matrix4x4[_nodes.Count];
            _world = new Matrix4x4[_nodes.Count];
            Skin = new Matrix4x4[_nodes.Count];

            Array.Fill(_slotNodes, -1);

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];

                if (node.ParentIndex >= i)
                    throw new InvalidDataException($"Node {i} precedes its parent {node.ParentIndex}");

                if (node.MatrixIndex >= _slotNodes.Length)
                    throw new InvalidDataException($"Node {i} uses matrix {node.MatrixIndex} of {_slotNodes.Length}");

                if (node.MatrixIndex >= 0)
                    _slotNodes[node.MatrixIndex] = i;

                _inverseBind[i] = node.InvInitMatrix;

                _bind[i] = new NodeTransform
                {
                    Translation = node.Translation,
                    Rotation = new Vector3(
                        BitConverter.SingleToInt32Bits(node.Rotation.X),
                        BitConverter.SingleToInt32Bits(node.Rotation.Y),
                        BitConverter.SingleToInt32Bits(node.Rotation.Z)) * AngleToRadians,
                    Scale = node.Scale,
                };
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].ParentIndex >= 0)
                    continue;

                if (!Matrix4x4.Invert(_inverseBind[i], out var bind) ||
                    !Matrix4x4.Invert(Local(_nodes[i].Type, _bind[i]), out var inverseLocal))
                {
                    throw new InvalidDataException($"Root node {i} has a singular bind transform");
                }

                _attachment[i] = inverseLocal * bind;
            }

            Pose(_bind);

            for (var i = 0; i < _nodes.Count; i++)
            {
                if (!IsNearIdentity(Skin[i], _world[i].Translation.Length()))
                    throw new InvalidDataException($"Node {i} ({_nodes[i].Type}) disagrees with its inverse bind matrix");
            }
        }

        public Matrix4x4[] Skin { get; }
        public ReadOnlySpan<NodeTransform> Bind => _bind;

        public int SlotNode(int slot)
        {
            if (slot < 0)
                return -1;

            if (slot >= _slotNodes.Length || _slotNodes[slot] < 0)
                throw new InvalidDataException($"No node uses matrix {slot}");

            return _slotNodes[slot];
        }

        public void Pose(ReadOnlySpan<NodeTransform> transforms)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var local = Local(node.Type, transforms[i]);

                _world[i] = node.ParentIndex >= 0 ? local * _world[node.ParentIndex] : local * _attachment[i];
                Skin[i] = _inverseBind[i] * _world[i];
            }
        }

        private static Matrix4x4 Local(NodeType type, in NodeTransform transform)
        {
            return Matrix4x4.CreateScale(transform.Scale)
                   * Rotation(type, transform.Rotation)
                   * Matrix4x4.CreateTranslation(transform.Translation);
        }

        private static Matrix4x4 Rotation(NodeType type, Vector3 radians)
        {
            var x = Matrix4x4.CreateRotationX(radians.X);
            var y = Matrix4x4.CreateRotationY(radians.Y);
            var z = Matrix4x4.CreateRotationZ(radians.Z);

            // Row vectors apply left to right, so XYZ rotates about X first.
            return (type & NodeType.NND_NODETYPE_ROTATE_TYPE_MASK) switch
            {
                NodeType.NND_NODETYPE_ROTATE_TYPE_XYZ => x * y * z,
                NodeType.NND_NODETYPE_ROTATE_TYPE_XZY => x * z * y,
                NodeType.NND_NODETYPE_ROTATE_TYPE_YXZ => y * x * z,
                NodeType.NND_NODETYPE_ROTATE_TYPE_YZX => y * z * x,
                NodeType.NND_NODETYPE_ROTATE_TYPE_ZXY => z * x * y,
                NodeType.NND_NODETYPE_ROTATE_TYPE_ZYX => z * y * x,
                var other => throw new NotSupportedException($"Rotate type {other}"),
            };
        }

        private static bool IsNearIdentity(Matrix4x4 matrix, float distance)
        {
            const float Epsilon = 1e-3f;

            var difference = matrix - Matrix4x4.Identity;

            for (var row = 0; row < 4; row++)
            {
                // Translation error grows with distance from the origin.
                var tolerance = row == 3 ? Epsilon * MathF.Max(1.0f, distance) : Epsilon;

                for (var column = 0; column < 4; column++)
                {
                    if (!(MathF.Abs(difference[row, column]) <= tolerance))
                        return false;
                }
            }

            return true;
        }
    }
}
