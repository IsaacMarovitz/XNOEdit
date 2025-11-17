using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Pfim;
using Silk.NET.WebGPU;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit
{
    public class UIManager : IDisposable
    {
        public Action ResetCameraAction;

        private static WebGPU _wgpu;
        private static WgpuDevice _device;
        private static ImGuiController _controller;
        private static ImGuiXnoPanel _xnoPanel;
        private static ImGuiAlertPanel _alertPanel;
        private static readonly Dictionary<string, IntPtr> Textures = [];

        private static bool _xnoWindow = true;
        private static bool _environmentWindow = true;
        private static float _sunAzimuth;
        private static float _sunAltitude;

        public void OnLoad(
            WebGPU wgpu,
            WgpuDevice device,
            ImGuiController controller,
            RenderSettings settings)
        {
            _wgpu = wgpu;
            _device = device;
            _controller = controller;
            _alertPanel = new ImGuiAlertPanel();

            _sunAltitude = MathF.Asin(settings.SunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(settings.SunDirection.Z, settings.SunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        public unsafe void OnRender(double deltaTime, ref RenderSettings settings, RenderPassEncoder* pass)
        {
            _controller.Update((float)deltaTime);

            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingOverCentralNode);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Render"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("Show Grid", "G", ref settings.ShowGrid);
                    ImGui.MenuItem("Vertex Colors", "V", ref settings.VertexColors);
                    ImGui.MenuItem("Backface Culling", "C", ref settings.BackfaceCulling);
                    ImGui.MenuItem("Wireframe", "F", ref settings.WireframeMode);

                    ImGui.Separator();

                    if (ImGui.MenuItem("Reset Camera", "R"))
                    {
                        ResetCameraAction?.Invoke();
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("XNO", null, ref _xnoWindow);
                    ImGui.MenuItem("Environment", null, ref _environmentWindow);

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (_environmentWindow)
            {
                ImGui.Begin("Environment");
                ImGui.SeparatorText("Sun");
                ImGui.ColorEdit3("Color", ref settings.SunColor, ImGuiColorEditFlags.NoInputs);

                var editedAzimuth = ImGui.SliderFloat("Azimuth", ref _sunAzimuth, 0.0f, 360.0f, "%.1f°");
                var editedAltitude = ImGui.SliderFloat("Altitude", ref _sunAltitude, 0.0f, 90.0f, "%.1f°");

                if (editedAzimuth || editedAltitude)
                {
                    var azimuthRad = _sunAzimuth * MathF.PI / 180.0f;
                    var altitudeRad = _sunAltitude * MathF.PI / 180.0f;

                    settings.SunDirection = new Vector3(
                        (float)(Math.Cos(altitudeRad) * Math.Cos(azimuthRad)),
                        (float)Math.Sin(altitudeRad),
                        (float)(Math.Cos(altitudeRad) * Math.Sin(azimuthRad)));
                }

                ImGui.End();
            }

            if (_xnoPanel != null)
            {
                if (_xnoWindow)
                    _xnoPanel.Render(Textures);
            }
            else
            {
                ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Text("Drag and drop a .xno file");
                ImGui.End();
            }

            _alertPanel.Render(deltaTime);
            _controller.Render(pass);
        }

        public unsafe void ReadXno(NinjaNext xno, string file, Queue* queue)
        {
            _xnoPanel = new ImGuiXnoPanel(xno);


            foreach (var texturePtr in Textures.Values)
            {
                _controller.UnbindImGuiTextureView((TextureView*)texturePtr);
                _wgpu.TextureViewRelease((TextureView*)texturePtr);
            }

            Textures.Clear();

            var textureListChunk = xno.GetChunk<TextureListChunk>();
            if (textureListChunk != null)
            {
                Console.WriteLine($"Loading {textureListChunk.Textures.Count} textures...");
                foreach (var texture in textureListChunk.Textures)
                {
                    var folderPath = Path.GetDirectoryName(file);
                    var texturePath = Path.Combine(folderPath, texture.Name);

                    if (!File.Exists(texturePath))
                    {
                        Console.WriteLine($"  Warning: Texture not found: {texture.Name}");
                        continue;
                    }

                    using var image = Pfimage.FromFile(texturePath);

                    var textureDesc = new TextureDescriptor
                    {
                        Size = new Extent3D
                        {
                            Width = (uint)image.Width,
                            Height = (uint)image.Height,
                            DepthOrArrayLayers = 1
                        },
                        MipLevelCount = 1,
                        SampleCount = 1,
                        Dimension = TextureDimension.Dimension2D,
                        Format = TextureFormat.Bgra8Unorm,
                        Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                    };

                    var wgpuTexture = _wgpu.DeviceCreateTexture(_device, &textureDesc);

                    var imageData = image.Data;
                    if (image.Format == ImageFormat.Rgb24)
                    {
                        var rgbaData = new byte[image.Width * image.Height * 4];
                        for (int i = 0, j = 0; i < imageData.Length; i += 3, j += 4)
                        {
                            rgbaData[j] = imageData[i];         // R
                            rgbaData[j + 1] = imageData[i + 1]; // G
                            rgbaData[j + 2] = imageData[i + 2]; // B
                            rgbaData[j + 3] = 255;              // A
                        }
                        imageData = rgbaData;
                    }

                    fixed (byte* pData = imageData)
                    {
                        var imageCopyTexture = new ImageCopyTexture
                        {
                            Texture = wgpuTexture,
                            MipLevel = 0,
                            Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
                            Aspect = TextureAspect.All
                        };

                        var textureDataLayout = new TextureDataLayout
                        {
                            Offset = 0,
                            BytesPerRow = (uint)(image.Width * 4),
                            RowsPerImage = (uint)image.Height
                        };

                        var writeSize = new Extent3D
                        {
                            Width = (uint)image.Width,
                            Height = (uint)image.Height,
                            DepthOrArrayLayers = 1
                        };

                        _wgpu.QueueWriteTexture(queue, &imageCopyTexture, pData, (nuint)(image.Width * image.Height * 4), &textureDataLayout, &writeSize);
                    }

                    var viewDesc = new TextureViewDescriptor
                    {
                        Format = TextureFormat.Bgra8Unorm,
                        Dimension = TextureViewDimension.Dimension2D,
                        BaseMipLevel = 0,
                        MipLevelCount = 1,
                        BaseArrayLayer = 0,
                        ArrayLayerCount = 1,
                        Aspect = TextureAspect.All
                    };

                    var textureView = _wgpu.TextureCreateView(wgpuTexture, &viewDesc);
                    _controller.BindImGuiTextureView(textureView);

                    Textures.Add(texture.Name, (IntPtr)textureView);
                }
            }
        }

        public unsafe void Dispose()
        {
            foreach (var texturePtr in Textures.Values)
            {
                _wgpu.TextureViewRelease((TextureView*)texturePtr);
            }

            _controller?.Dispose();
        }

        public unsafe void TriggerAlert(string alert)
        {
            if (alert != string.Empty)
                _alertPanel.TriggerAlert(alert);
        }
    }
}
