using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using XNOEdit.Managers;
using XNOEdit.Services;

namespace XNOEdit.Panels
{
    public class XnoPanel
    {
        public const string Name = "XNO";
        private readonly NinjaNext _xno;
        private readonly ISceneVisibility _visibility;
        private readonly int _xnoIndex;

        public XnoPanel(NinjaNext xno, ISceneVisibility visibility, int xnoIndex = 0)
        {
            _xno = xno;
            _visibility = visibility;
            _xnoIndex = xnoIndex;
        }

        private bool GetSubobjectVisibility(int subobjectIndex)
        {
            return _visibility.GetSubobjectVisible(_xnoIndex, subobjectIndex);
        }

        private void SetSubobjectVisibility(int subobjectIndex, bool visible)
        {
            _visibility.SetSubobjectVisible(_xnoIndex, subobjectIndex, visible);
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
            ImGui.Text(_xno.Name);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                var objectChunk = _xno.GetChunk<ObjectChunk>();
                var textureListChunk = _xno.GetChunk<TextureListChunk>();
                var effectListChunk = _xno.GetChunk<EffectListChunk>();

                if (objectChunk != null)
                {
                    RenderObjectChunk(objectChunk, effectListChunk);
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

        private unsafe void RenderObjectChunk(ObjectChunk objectChunk, EffectListChunk? effectListChunk)
        {
            if (ImGui.BeginTabItem("Object"))
            {
                var center = objectChunk.Centre;
                ImGui.InputFloat3("Center", ref center, "%.1f", ImGuiInputTextFlags.ReadOnly);

                if (objectChunk.BoundingBox.HasValue)
                {
                    var boundingBox = objectChunk.BoundingBox.Value;
                    ImGui.InputFloat3("Bounding Box", ref boundingBox, "%.1f", ImGuiInputTextFlags.ReadOnly);
                }

                var radius = objectChunk.Radius;
                ImGui.InputFloat("Radius", ref radius, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

                ImGui.Text($"Texture Count: {objectChunk.TextureCount}");
                ImGui.Text($"Subobject Count: {objectChunk.SubObjects.Count}");
                ImGui.Text($"Material Count: {objectChunk.Materials.Count}");

                ImGui.SeparatorText("Subobjects");
                for (var i = 0; i < objectChunk.SubObjects.Count; i++)
                {
                    ImGui.PushID(i);

                    var visible = GetSubobjectVisibility(i);
                    if (ImGuiComponents.StyledCheckbox($"##VisibilitySubobject{i + 1}", visible))
                    {
                        visible = !visible;
                        SetSubobjectVisibility(i, visible);
                    }

                    ImGui.SameLine();

                    if (ImGui.CollapsingHeader($"Subobject {i + 1}", ImGuiTreeNodeFlags.AllowOverlap))
                    {
                        var subobject = objectChunk.SubObjects[i];

                        ImGui.Text($"Type: {subobject.Type}");

                        ImGui.SeparatorText("Mesh Sets");

                        ImGui.Indent();

                        for (var j = 0; j < subobject.MeshSets.Count; j++)
                        {
                            ImGui.PushID(j);

                            var visibleMeshSet = GetMeshSetVisibility(i, j);

                            if (ImGuiComponents.StyledCheckbox($"##VisibilityMeshSet{j + 1}", visibleMeshSet))
                            {
                                visibleMeshSet = !visibleMeshSet;
                                SetMeshSetVisibility(i, j, visibleMeshSet);
                            }

                            ImGui.SameLine();

                            if (ImGui.CollapsingHeader($"Mesh Set {j + 1}", ImGuiTreeNodeFlags.AllowOverlap))
                            {
                                RenderMeshSet(subobject.MeshSets[j], effectListChunk);
                            }

                            ImGui.PopID();
                        }

                        ImGui.Unindent();
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
            ImGui.InputFloat3("Center", ref meshCenter, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var meshRadius = meshSet.Radius;
            ImGui.InputFloat("Radius", ref meshRadius, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

            var techniqueShown = false;

            if (effectListChunk != null)
            {
                var effect = meshSet.GetTechnique(effectListChunk);

                if (effect != null)
                {
                    ImGui.Text($"Technique: {effect.Name}");
                    techniqueShown = true;
                }
            }

            ImGui.SeparatorText("Indices");
            ImGui.Text($"Material: {meshSet.MaterialIndex}");
            if (!techniqueShown)
                ImGui.Text($"Technique: {meshSet.TechniqueIndex}");
            ImGui.Text($"Matrix: {meshSet.MatrixIndex}");
            ImGui.Text($"Node: {meshSet.NodeIndex}");
            ImGui.Text($"Primitive List: {meshSet.PrimitiveListIndex}");
            ImGui.Text($"Vertex List: {meshSet.VertexListIndex}");
        }

        private static void RenderMaterial(Material material)
        {
            var ambient = PropertyUtility.MaterialColorToVec4(material.Colour.Ambient);
            var diffuse = PropertyUtility.MaterialColorToVec4(material.Colour.Diffuse);
            var specular = PropertyUtility.MaterialColorToVec4(material.Colour.Specular);
            var emissive = PropertyUtility.MaterialColorToVec4(material.Colour.Emissive);

            var power = material.Colour.Power;

            ImGui.SeparatorText("Color");
            ImGui.ColorEdit4("Ambient", ref ambient, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Diffuse", ref diffuse, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Specular", ref specular, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Emissive", ref emissive, ImGuiColorEditFlags.NoInputs);
            ImGui.InputFloat("Power", ref power, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

            ImGui.SeparatorText("Logic");

            var blend = material.Logic.Blend;
            ImGui.Checkbox("Blend", ref blend);
            ImGui.SameLine();

            var alpha = material.Logic.Alpha;
            ImGui.Checkbox("Alpha", ref alpha);

            var zCompare = material.Logic.ZCompare;
            ImGui.Checkbox("Z Compare", ref zCompare);
            ImGui.SameLine();

            var zUpdate = material.Logic.ZUpdate;
            ImGui.Checkbox("Z Update", ref zUpdate);

            ImGui.Text($"Source Blend: {PropertyUtility.BlendModeToString(material.Logic.SourceBlend)}");
            ImGui.Text($"Destination Blend: {PropertyUtility.BlendModeToString(material.Logic.DestinationBlend)}");
            ImGui.Text($"Blend Factor: {material.Logic.BlendFactor}");
            ImGui.Text($"Blend Operation: {PropertyUtility.BlendOperationToString(material.Logic.BlendOperation)}");
            ImGui.Text($"Logic Operation: {PropertyUtility.LogicOperationToString(material.Logic.LogicOperation)}");
            ImGui.Text($"Alpha Ref: {material.Logic.AlphaRef}");
            ImGui.Text($"Alpha Compare Function: {PropertyUtility.CompareFunctionToString(material.Logic.AlphaFunction)}");
            ImGui.Text($"Z Compare Function: {PropertyUtility.CompareFunctionToString(material.Logic.ZCompareFunction)}");
        }

        private unsafe void RenderTextureChunk(TextureManager textureManager, TextureListChunk textureListChunk)
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

                        var textureId = textureManager.GetImGuiId(texture.Name);
                        if (textureId != 0)
                        {
                            ImGui.Image(new ImTextureRef(null, textureId), new Vector2(150, 150));
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
    }
}
