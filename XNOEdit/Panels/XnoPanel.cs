using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.Services;

namespace XNOEdit.Panels
{
    public class XnoPanel
    {
        public const string Name = "XNO";
        private readonly NinjaNext _xno;
        private readonly MaterialMotionChunk? _materialMotion;
        private readonly ISceneVisibility _visibility;
        private readonly int _xnoIndex;

        private readonly record struct MeshSetEntry(int SubobjectIndex, int MeshSetIndex, MeshSet MeshSet);

        public XnoPanel(NinjaNext xno, MaterialMotionChunk? materialMotion, ISceneVisibility visibility, int xnoIndex = 0)
        {
            _xno = xno;
            _materialMotion = materialMotion;
            _visibility = visibility;
            _xnoIndex = xnoIndex;
        }

        private bool GetMeshSetsVisibility(IEnumerable<MeshSetEntry> entries)
        {
            return entries.All(x => GetMeshSetVisibility(x.SubobjectIndex, x.MeshSetIndex));
        }

        private void SetMeshSetsVisibility(IEnumerable<MeshSetEntry> entries, bool visible)
        {
            foreach (var entry in entries)
            {
                SetMeshSetVisibility(entry.SubobjectIndex, entry.MeshSetIndex, visible);
            }
        }

        private bool GetMeshSetVisibility(int subobjectIndex, int meshSetIndex)
        {
            return _visibility.GetMeshSetVisible(_xnoIndex, subobjectIndex, meshSetIndex);
        }

        private void SetMeshSetVisibility(int subobjectIndex, int meshSetIndex, bool visible)
        {
            _visibility.SetMeshSetVisible(_xnoIndex, subobjectIndex, meshSetIndex, visible);
        }

        public void Render(TextureManager textureManager)
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);

            ImGui.Text("File Name:");
            ImGui.Text($"{_xno.Name ?? "<Unknown>"}");

            if (ImGui.BeginTabBar("Tab Bar"))
            {
                var objectChunk = _xno.GetChunk<ObjectChunk>();
                var textureListChunk = _xno.GetChunk<TextureListChunk>();
                var effectListChunk = _xno.GetChunk<EffectListChunk>();
                var nodeNameChunk = _xno.GetChunk<NodeNameChunk>();

                if (objectChunk != null)
                {
                    RenderObjectChunk(objectChunk, effectListChunk, nodeNameChunk);
                }

                if (_materialMotion != null)
                {
                    RenderMaterialMotionChunk(_materialMotion, nodeNameChunk);
                }

                if (textureListChunk != null)
                {
                    RenderTextureChunk(textureManager, textureListChunk);
                }

                if (effectListChunk != null)
                {
                    RenderEffectChunk(effectListChunk);
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void RenderObjectChunk(ObjectChunk objectChunk, EffectListChunk? effectListChunk, NodeNameChunk? nodeNameChunk)
        {
            if (ImGui.BeginTabItem("Object"))
            {
                var center = objectChunk.Centre;
                ImGuiComponents.InputFloat3("Center", ref center, "%.1f", ImGuiInputTextFlags.ReadOnly);

                if (objectChunk.BoundingBox.HasValue)
                {
                    var boundingBox = objectChunk.BoundingBox.Value;
                    ImGuiComponents.InputFloat3("Bounding Box", ref boundingBox, "%.1f", ImGuiInputTextFlags.ReadOnly);
                }

                var radius = objectChunk.Radius;
                ImGuiComponents.InputFloat("Radius", ref radius, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

                ImGui.Text($"Texture Count: {objectChunk.TextureCount}");
                ImGui.Text($"Subobject Count: {objectChunk.SubObjects.Count}");
                ImGui.Text($"Material Count: {objectChunk.Materials.Count}");

                var meshSetsByNode = objectChunk.SubObjects
                    .SelectMany((subobject, i) => subobject.MeshSets
                        .Select((meshSet, j) => new MeshSetEntry(i, j, meshSet)))
                    .ToLookup(x => x.MeshSet.NodeIndex);

                ImGui.SeparatorText("Nodes");
                foreach (var i in Enumerable.Range(0, objectChunk.Nodes.Count).OrderByDescending(meshSetsByNode.Contains))
                {
                    ImGui.PushID(i);

                    var meshSets = meshSetsByNode[i];
                    var visible = GetMeshSetsVisibility(meshSets);

                    ImGui.BeginDisabled(!meshSets.Any());
                    if (ImGuiComponents.StyledCheckbox($"##VisibilityNode{i + 1}", visible))
                    {
                        SetMeshSetsVisibility(meshSets, !visible);
                    }
                    ImGui.EndDisabled();

                    ImGui.SameLine();

                    if (ImGui.CollapsingHeader(PropertyUtility.GetNodeName(nodeNameChunk, i), ImGuiTreeNodeFlags.AllowOverlap))
                    {
                        RenderNode(objectChunk, objectChunk.Nodes[i], meshSets.GroupBy(x => x.SubobjectIndex), effectListChunk);
                    }

                    ImGui.PopID();
                }

                ImGui.SeparatorText("Materials");
                for (var i = 0; i < objectChunk.Materials.Count; i++)
                {
                    ImGui.PushID(i);

                    if (ImGui.CollapsingHeader($"Material {i + 1}"))
                    {
                        RenderMaterial(objectChunk.Materials[i]);
                    }

                    ImGui.PopID();
                }

                ImGui.EndTabItem();
            }
        }

        private void RenderMeshSet(MeshSet meshSet, EffectListChunk? effectListChunk)
        {
            var meshCenter = meshSet.Centre;
            ImGuiComponents.InputFloat3("Center", ref meshCenter, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var meshRadius = meshSet.Radius;
            ImGuiComponents.InputFloat("Radius", ref meshRadius, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

            string? techniqueName = null;

            if (effectListChunk != null)
            {
                var effect = meshSet.GetTechnique(effectListChunk);

                if (effect != null)
                {
                    techniqueName = effect.Name;
                }
            }

            ImGui.SeparatorText("Indices");
            ImGui.Text($"Material: {meshSet.MaterialIndex + 1}");
            ImGui.Text($"Technique: {techniqueName ?? "<Unknown>"} ({meshSet.TechniqueIndex + 1})");
            ImGui.Text($"Matrix: {meshSet.MatrixIndex + 1}");
            ImGui.Text($"Primitive List: {meshSet.PrimitiveListIndex + 1}");
            ImGui.Text($"Vertex List: {meshSet.VertexListIndex + 1}");
        }

        private void RenderNode(ObjectChunk objectChunk, Node node, IEnumerable<IGrouping<int, MeshSetEntry>> subobjects, EffectListChunk? effectListChunk)
        {
            var translation = node.Translation;
            ImGuiComponents.InputFloat3("Translation", ref translation, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var center = node.Center;
            ImGuiComponents.InputFloat3("Center", ref center, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var boundingBox = node.BoundingBox;
            ImGuiComponents.InputFloat3("Bounding Box", ref boundingBox, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var rotation = node.Rotation;
            ImGuiComponents.InputFloat3("Rotation", ref rotation, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var scale = node.Scale;
            ImGuiComponents.InputFloat3("Scale", ref scale, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var radius = node.Radius;
            ImGuiComponents.InputFloat("Radius", ref radius, "%.1f", ImGuiInputTextFlags.ReadOnly);

            if (!subobjects.Any())
                return;

            ImGui.Indent();

            foreach (var subobject in subobjects)
            {
                ImGui.PushID(subobject.Key);

                var visible = GetMeshSetsVisibility(subobject);
                if (ImGuiComponents.StyledCheckbox($"##VisibilitySubobject{subobject.Key + 1}", visible))
                {
                    SetMeshSetsVisibility(subobject, !visible);
                }

                ImGui.SameLine();
                ImGui.Text($"{(GuestDrawBucket)(objectChunk.SubObjects[subobject.Key].Type & 0xFF)} {subobject.Key + 1}");

                foreach (var entry in subobject)
                {
                    ImGui.PushID(entry.MeshSetIndex);
                    ImGui.Indent();

                    var visibleMeshSet = GetMeshSetVisibility(entry.SubobjectIndex, entry.MeshSetIndex);
                    if (ImGuiComponents.StyledCheckbox($"##VisibilityMeshSet{entry.MeshSetIndex + 1}", visibleMeshSet))
                    {
                        SetMeshSetVisibility(entry.SubobjectIndex, entry.MeshSetIndex, !visibleMeshSet);
                    }

                    ImGui.SameLine();

                    if (ImGui.CollapsingHeader($"Mesh Set {entry.MeshSetIndex + 1}", ImGuiTreeNodeFlags.AllowOverlap))
                    {
                        RenderMeshSet(entry.MeshSet, effectListChunk);
                    }

                    ImGui.Unindent();
                    ImGui.PopID();
                }

                ImGui.PopID();
            }

            ImGui.Unindent();
        }

        private static void RenderMaterial(Material material)
        {
            var ambient = PropertyUtility.MaterialColorToVec4(material.Colour.Ambient);
            var diffuse = PropertyUtility.MaterialColorToVec4(material.Colour.Diffuse);
            var specular = PropertyUtility.MaterialColorToVec4(material.Colour.Specular);
            var emissive = PropertyUtility.MaterialColorToVec4(material.Colour.Emissive);

            var power = material.Colour.Power;

            ImGui.SeparatorText("Color");
            ImGuiComponents.ColorEdit4("Ambient", ref ambient, ImGuiColorEditFlags.NoInputs);
            ImGuiComponents.ColorEdit4("Diffuse", ref diffuse, ImGuiColorEditFlags.NoInputs);
            ImGuiComponents.ColorEdit4("Specular", ref specular, ImGuiColorEditFlags.NoInputs);
            ImGuiComponents.ColorEdit4("Emissive", ref emissive, ImGuiColorEditFlags.NoInputs);
            ImGuiComponents.InputFloat("Power", ref power, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

            ImGui.SeparatorText("Logic");

            var blend = material.Logic.Blend;
            ImGuiComponents.Checkbox("Blend", ref blend);
            ImGui.SameLine();

            var alpha = material.Logic.Alpha;
            ImGuiComponents.Checkbox("Alpha", ref alpha);

            var zCompare = material.Logic.ZCompare;
            ImGuiComponents.Checkbox("Z Compare", ref zCompare);
            ImGui.SameLine();

            var zUpdate = material.Logic.ZUpdate;
            ImGuiComponents.Checkbox("Z Update", ref zUpdate);

            ImGui.Text($"Source Blend: {PropertyUtility.BlendModeToString(material.Logic.SourceBlend)}");
            ImGui.Text($"Destination Blend: {PropertyUtility.BlendModeToString(material.Logic.DestinationBlend)}");
            ImGui.Text($"Blend Factor: {material.Logic.BlendFactor}");
            ImGui.Text($"Blend Operation: {PropertyUtility.BlendOperationToString(material.Logic.BlendOperation)}");
            ImGui.Text($"Logic Operation: {PropertyUtility.LogicOperationToString(material.Logic.LogicOperation)}");
            ImGui.Text($"Alpha Ref: {material.Logic.AlphaRef}");
            ImGui.Text($"Alpha Compare Function: {PropertyUtility.CompareFunctionToString(material.Logic.AlphaFunction)}");
            ImGui.Text($"Z Compare Function: {PropertyUtility.CompareFunctionToString(material.Logic.ZCompareFunction)}");
        }

        private void RenderTextureChunk(TextureManager textureManager, TextureListChunk textureListChunk)
        {
            if (ImGui.BeginTabItem("Texture List"))
            {
                foreach (var texture in textureListChunk.Textures)
                {
                    if (ImGui.CollapsingHeader(texture.Name))
                    {
                        ImGui.Text($"Bank: {texture.Bank}");
                        ImGui.Text($"Global Index: {texture.GlobalIndex}");

                        var minFilter = PropertyUtility.MinFilterToString(texture.MinFilter);
                        ImGui.Text($"Min Filter: {minFilter.Item1}");
                        ImGui.Text($"Mag Filter: {PropertyUtility.MagFilterToString(texture.MagFilter)}");

                        if (minFilter.Item2 != null)
                        {
                            ImGui.Text($"Mipmap Filter: {minFilter.Item2}");
                        }

                        ImGui.Text($"Type: {texture.Type}");

                        var textureId = textureManager.GetImGuiTextureId(texture.Name);
                        if (textureId != 0)
                        {
                            ImGuiInterop.Image(textureId, new Vector2(150, 150));
                        }
                    }
                }

                ImGui.EndTabItem();
            }
        }

        private void RenderEffectChunk(EffectListChunk effectListChunk)
        {
            if (ImGui.BeginTabItem("Effect List"))
            {
                var uniqueEffects = effectListChunk.Effects
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .GroupBy(x => x.Name)
                    .Select(g => g.FirstOrDefault());

                foreach (var effect in uniqueEffects)
                {
                    if (ImGui.CollapsingHeader(effect.Name))
                    {
                        ImGui.Text("Techniques:");

                        foreach (var technique in effectListChunk.Techniques)
                        {
                            if (technique.GetEffect(effectListChunk).Name == effect.Name)
                            {
                                ImGui.BulletText(technique.Name);
                            }
                        }
                    }
                }

                ImGui.EndTabItem();
            }
        }

        private void RenderMaterialMotionChunk(MaterialMotionChunk materialMotionChunk, NodeNameChunk? nodeNameChunk)
        {
            if (ImGui.BeginTabItem("Motion"))
            {
                ImGui.Text($"Type: {PropertyUtility.MotionTypeToString(materialMotionChunk.Type)}");
                ImGui.Text($"Start Frame: {materialMotionChunk.StartFrame}");
                ImGui.Text($"End Frame: {materialMotionChunk.EndFrame}");
                ImGui.Text($"FPS: {materialMotionChunk.FPS}");

                if (ImGui.CollapsingHeader("Submotions", ImGuiTreeNodeFlags.AllowOverlap))
                {
                    var subMotionGroups = materialMotionChunk.SubMotions
                        .GroupBy(x => x.NodeIndex)
                        .OrderBy(x => x.Key);

                    foreach (var subMotionGroup in subMotionGroups)
                    {
                        if (!ImGui.TreeNode($"{PropertyUtility.GetNodeName(nodeNameChunk, subMotionGroup.Key)}"))
                            continue;

                        var first = true;

                        foreach (var subMotion in subMotionGroup.OrderBy(x => x.StartKeyframe))
                        {
                            if (!first)
                                ImGui.Separator();

                            first = false;

                            ImGui.Text($"Type: {PropertyUtility.SubmotionTypeToString(subMotion.Type)}");
                            ImGui.Text($"Interpolation Type: {PropertyUtility.SubmotionInterpolationTypeToString(subMotion.InterpolationType)}");
                            ImGui.Text($"Start Keyframe: {subMotion.StartKeyframe}");
                            ImGui.Text($"End Keyframe: {subMotion.EndKeyframe}");

                            foreach (var keyframe in subMotion.Keyframes)
                            {
                                RenderKeyframe(keyframe);
                            }
                        }

                        ImGui.TreePop();
                    }
                }

                ImGui.EndTabItem();
            }
        }

        private void RenderKeyframe(object keyframe)
        {
            switch (keyframe)
            {
                case KeyframeF32 keyframeF32:
                    ImGui.Text($"Frame {keyframeF32.Frame}: {keyframeF32.Value}");
                    break;
                case KeyframeS16 keyframeS16:
                    ImGui.Text($"Frame {keyframeS16.Frame}: {keyframeS16.Value}");
                    break;
                case KeyframeVector keyframeVector:
                    ImGui.Text($"Frame {keyframeVector.Frame}: ({keyframeVector.Value.X}, {keyframeVector.Value.Y}, {keyframeVector.Value.Z})");
                    break;
                case KeyframeRotateS16 keyframeRotateS16:
                    ImGui.Text($"Frame {keyframeRotateS16.Frame}: ({keyframeRotateS16.X}, {keyframeRotateS16.Y}, {keyframeRotateS16.Z})");
                    break;
                default:
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Unknown keyframe type {keyframe.GetType().Name}!");
                    break;
            }
        }
    }
}
