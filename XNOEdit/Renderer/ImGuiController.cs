using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Plume;
using SDL3;
using Solaris;
using Solaris.Graph;

namespace XNOEdit.Renderer
{
    public unsafe class ImGuiController : IDisposable
    {
        private readonly SlDevice _device;
        private readonly SlUploader _uploader;
        private readonly IntPtr _window;

        private SlMaterial _material;
        private SlSamplerIndex _fontSampler;

        private readonly Dictionary<nint, (SlTexture Texture, SlTextureIndex Index)> _gpuTextures = [];

        private SetClipboardTextDelegate _setClipboardText;
        private GetClipboardTextDelegate _getClipboardText;
        private IntPtr _clipboardText;

        private static readonly SlPipelineVariant Variant = new()
        {
            Topology = RenderPrimitiveTopology.TriangleList,
            CullMode = RenderCullMode.None,
            FrontFace = RenderFrontFace.Clockwise,
            DepthWrite = false,
            DepthTest = false,
            Blend = SlBlendState.AlphaBlend
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ImGuiTexturePush
        {
            public uint TextureIndex;
            public uint SamplerIndex;
        }

        public static class ImGuiPushConstants
        {
            public const uint MvpOffset = 0;
            public const uint TextureOffset = 64;
        }

        public ImGuiController(
            SlDevice device,
            SlUploader uploader,
            IntPtr window)
        {
            _device = device;
            _uploader = uploader;
            _window = window;

            Init();
        }

        public void Update(float delta)
        {
            SetPerFrameImGuiData(delta);
            ImGui.NewFrame();
        }

        public void Render(SlPassContext ctx)
        {
            DrawImGui(ctx);
        }

        private void Init()
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            SDL.GetWindowSize(_window, out var logicalWidth, out var localHeight);
            SDL.GetWindowSizeInPixels(_window, out var width, out var height);

            ImGui.GetIO().DisplaySize = new Vector2(logicalWidth, localHeight);
            ImGui.GetIO().DisplayFramebufferScale = new Vector2((float)width / logicalWidth, (float)height / localHeight);

            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasTextures;

            var platformIO = ImGui.GetPlatformIO();

            _setClipboardText = SetClipboardText;
            _getClipboardText = GetClipboardText;

            platformIO.PlatformSetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(_setClipboardText);
            platformIO.PlatformGetClipboardTextFn = (void*)Marshal.GetFunctionPointerForDelegate(_getClipboardText);

            InitSampler();
            InitMaterial(ShaderLibrary.Get(_device, "imgui_vs"),
                ShaderLibrary.Get(_device, "imgui_ps"));

            SetPerFrameImGuiData(1f / 60f);
        }

        private void InitSampler()
        {
            SlSamplerDescriptor samplerDescriptor = new()
            {
                MinFilter = RenderFilter.Linear,
                MagFilter = RenderFilter.Linear,
                MipmapMode = RenderMipmapMode.Linear,
                AddressU = RenderTextureAddressMode.Wrap,
                AddressV = RenderTextureAddressMode.Wrap,
                AddressW = RenderTextureAddressMode.Wrap,
                MaxAnisotropy = 1,
            };

            _fontSampler = _device.GetSampler(samplerDescriptor);
        }

        private void InitMaterial(ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> pixel)
        {
            _fontSampler = _device.GetSampler(SlSamplerDescriptor.LinearWrap with { MaxAnisotropy = 1 });

            var vertexShader = SlShader.Create(_device, vertex, "shaderMain");
            var pixelShader = SlShader.Create(_device, pixel, "shaderMain");

            var layout = new SlVertexLayout(
                new SlVertexBufferLayout(0, (uint)sizeof(ImDrawVert), SlVertexStepMode.Vertex,
                [
                    new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32Float, 0),
                    new SlVertexAttribute("TEXCOORD", 0, 1, RenderFormat.R32G32Float, 8),
                    new SlVertexAttribute("COLOR", 0, 2, RenderFormat.R8G8B8A8Unorm, 16),
                ]));

            _material = new SlMaterial(_device, vertexShader, pixelShader, layout);
        }

        public void PrepareFrame()
        {
            var drawData = ImGui.GetDrawData();

            for (var i = 0; i < drawData.Textures.Size; i++)
            {
                var texture = drawData.Textures[i];

                if (texture.Status != ImTextureStatus.Ok)
                    ProcessTextureRequest(texture);
            }
        }

        private void ProcessTextureRequest(ImTextureDataPtr texture)
        {
            switch (texture.Status)
            {
                case ImTextureStatus.WantCreate:
                    CreateTexture(texture);
                    break;
                case ImTextureStatus.WantUpdates:
                    UpdateTexture(texture);
                    break;
                case ImTextureStatus.WantDestroy:
                    DestroyTexture(texture);
                    break;
            }
        }

        private void CreateTexture(ImTextureDataPtr tex)
        {
            var width = (uint)tex.Width;
            var height = (uint)tex.Height;
            var pixels = (byte*)tex.GetPixels();

            var texture = _device.CreateTexture(
                SlTextureDescriptor.Sampled2D(width, height, RenderFormat.R8G8B8A8Unorm), "ImGuiAtlas");

            _uploader.StageTexture(
                texture,
                new ReadOnlySpan<byte>(pixels, (int)(width * height * 4)),
                width, height,
                bytesPerRow: width * 4);

            var index = _device.Tables.Register(texture);

            _gpuTextures[(nint)index.Packed] = (texture, index);

            tex.SetTexID(index.Packed);
            tex.SetStatus(ImTextureStatus.Ok);
        }

        private void UpdateTexture(ImTextureDataPtr tex)
        {
            var id = (nint)tex.TexID;

            if (id == 0 || !_gpuTextures.TryGetValue(id, out var entry))
            {
                CreateTexture(tex);
                return;
            }

            var rect = tex.UpdateRect;

            if (rect.W <= 0 || rect.H <= 0)
            {
                tex.SetStatus(ImTextureStatus.Ok);
                return;
            }

            var pitch = (uint)(tex.Width * tex.BytesPerPixel);
            var pixels = (byte*)tex.GetPixels();
            var origin = pixels + rect.Y * pitch + rect.X * tex.BytesPerPixel;
            var length = (rect.H - 1) * (int)pitch + rect.W * tex.BytesPerPixel;

            _uploader.StageTexture(
                entry.Texture,
                new ReadOnlySpan<byte>(origin, length),
                rect.W,
                rect.H,
                bytesPerRow: (uint)(rect.W * tex.BytesPerPixel),
                sourceRowPitch: pitch,
                destinationX: rect.X,
                destinationY: rect.Y);

            tex.SetStatus(ImTextureStatus.Ok);
        }

        private void DestroyTexture(ImTextureDataPtr tex)
        {
            var id = (nint)tex.TexID;

            if (id != 0 && _gpuTextures.Remove(id, out var entry))
            {
                _device.Tables.Release(entry.Index);
                _device.Retire(entry.Texture);
            }

            tex.SetTexID(null);
            tex.SetStatus(ImTextureStatus.Destroyed);
        }

        private static bool TryMapKeys(SDL.Keycode key, out ImGuiKey imguiKey)
        {
            imguiKey = key switch
            {
                SDL.Keycode.Tab => ImGuiKey.Tab,
                SDL.Keycode.Left => ImGuiKey.LeftArrow,
                SDL.Keycode.Right => ImGuiKey.RightArrow,
                SDL.Keycode.Up => ImGuiKey.UpArrow,
                SDL.Keycode.Down => ImGuiKey.DownArrow,
                SDL.Keycode.Pageup => ImGuiKey.PageUp,
                SDL.Keycode.Pagedown => ImGuiKey.PageDown,
                SDL.Keycode.Home => ImGuiKey.Home,
                SDL.Keycode.End => ImGuiKey.End,
                SDL.Keycode.Insert => ImGuiKey.Insert,
                SDL.Keycode.Delete => ImGuiKey.Delete,
                SDL.Keycode.Backspace => ImGuiKey.Backspace,
                SDL.Keycode.Space => ImGuiKey.Space,
                SDL.Keycode.Return => ImGuiKey.Enter,
                SDL.Keycode.Escape => ImGuiKey.Escape,
                SDL.Keycode.Apostrophe => ImGuiKey.Apostrophe,
                SDL.Keycode.Comma => ImGuiKey.Comma,
                SDL.Keycode.Minus => ImGuiKey.Minus,
                SDL.Keycode.Period => ImGuiKey.Period,
                SDL.Keycode.Slash => ImGuiKey.Slash,
                SDL.Keycode.Semicolon => ImGuiKey.Semicolon,
                SDL.Keycode.Equals => ImGuiKey.Equal,
                SDL.Keycode.LeftBracket => ImGuiKey.LeftBracket,
                SDL.Keycode.Backslash => ImGuiKey.Backslash,
                SDL.Keycode.RightBracket => ImGuiKey.RightBracket,
                SDL.Keycode.Grave => ImGuiKey.GraveAccent,
                SDL.Keycode.Capslock => ImGuiKey.CapsLock,
                SDL.Keycode.ScrollLock => ImGuiKey.ScrollLock,
                SDL.Keycode.NumLockClear => ImGuiKey.NumLock,
                SDL.Keycode.PrintScreen => ImGuiKey.PrintScreen,
                SDL.Keycode.Pause => ImGuiKey.Pause,
                SDL.Keycode.Kp0 => ImGuiKey.Keypad0,
                SDL.Keycode.Kp1 => ImGuiKey.Keypad1,
                SDL.Keycode.Kp2 => ImGuiKey.Keypad2,
                SDL.Keycode.Kp3 => ImGuiKey.Keypad3,
                SDL.Keycode.Kp4 => ImGuiKey.Keypad4,
                SDL.Keycode.Kp5 => ImGuiKey.Keypad5,
                SDL.Keycode.Kp6 => ImGuiKey.Keypad6,
                SDL.Keycode.Kp7 => ImGuiKey.Keypad7,
                SDL.Keycode.Kp8 => ImGuiKey.Keypad8,
                SDL.Keycode.Kp9 => ImGuiKey.Keypad9,
                SDL.Keycode.KpDecimal => ImGuiKey.KeypadDecimal,
                SDL.Keycode.KpDivide => ImGuiKey.KeypadDivide,
                SDL.Keycode.KpMultiply => ImGuiKey.KeypadMultiply,
                SDL.Keycode.KpMinus => ImGuiKey.KeypadSubtract,
                SDL.Keycode.KpPlus => ImGuiKey.KeypadAdd,
                SDL.Keycode.KpEnter => ImGuiKey.KeypadEnter,
                SDL.Keycode.KpEquals => ImGuiKey.KeypadEqual,
                SDL.Keycode.LCtrl => ImGuiKey.LeftCtrl,
                SDL.Keycode.LShift => ImGuiKey.LeftShift,
                SDL.Keycode.LAlt => ImGuiKey.LeftAlt,
                SDL.Keycode.LGUI => ImGuiKey.LeftSuper,
                SDL.Keycode.RCtrl => ImGuiKey.RightCtrl,
                SDL.Keycode.RShift => ImGuiKey.RightShift,
                SDL.Keycode.RAlt => ImGuiKey.RightAlt,
                SDL.Keycode.RGUI => ImGuiKey.RightSuper,
                SDL.Keycode.Menu => ImGuiKey.Menu,
                SDL.Keycode.Alpha0 => ImGuiKey.Key0,
                SDL.Keycode.Alpha1 => ImGuiKey.Key1,
                SDL.Keycode.Alpha2 => ImGuiKey.Key2,
                SDL.Keycode.Alpha3 => ImGuiKey.Key3,
                SDL.Keycode.Alpha4 => ImGuiKey.Key4,
                SDL.Keycode.Alpha5 => ImGuiKey.Key5,
                SDL.Keycode.Alpha6 => ImGuiKey.Key6,
                SDL.Keycode.Alpha7 => ImGuiKey.Key7,
                SDL.Keycode.Alpha8 => ImGuiKey.Key8,
                SDL.Keycode.Alpha9 => ImGuiKey.Key9,
                SDL.Keycode.A => ImGuiKey.A,
                SDL.Keycode.B => ImGuiKey.B,
                SDL.Keycode.C => ImGuiKey.C,
                SDL.Keycode.D => ImGuiKey.D,
                SDL.Keycode.E => ImGuiKey.E,
                SDL.Keycode.F => ImGuiKey.F,
                SDL.Keycode.G => ImGuiKey.G,
                SDL.Keycode.H => ImGuiKey.H,
                SDL.Keycode.I => ImGuiKey.I,
                SDL.Keycode.J => ImGuiKey.J,
                SDL.Keycode.K => ImGuiKey.K,
                SDL.Keycode.L => ImGuiKey.L,
                SDL.Keycode.M => ImGuiKey.M,
                SDL.Keycode.N => ImGuiKey.N,
                SDL.Keycode.O => ImGuiKey.O,
                SDL.Keycode.P => ImGuiKey.P,
                SDL.Keycode.Q => ImGuiKey.Q,
                SDL.Keycode.R => ImGuiKey.R,
                SDL.Keycode.S => ImGuiKey.S,
                SDL.Keycode.T => ImGuiKey.T,
                SDL.Keycode.U => ImGuiKey.U,
                SDL.Keycode.V => ImGuiKey.V,
                SDL.Keycode.W => ImGuiKey.W,
                SDL.Keycode.X => ImGuiKey.X,
                SDL.Keycode.Y => ImGuiKey.Y,
                SDL.Keycode.Z => ImGuiKey.Z,
                SDL.Keycode.F1 => ImGuiKey.F1,
                SDL.Keycode.F2 => ImGuiKey.F2,
                SDL.Keycode.F3 => ImGuiKey.F3,
                SDL.Keycode.F4 => ImGuiKey.F4,
                SDL.Keycode.F5 => ImGuiKey.F5,
                SDL.Keycode.F6 => ImGuiKey.F6,
                SDL.Keycode.F7 => ImGuiKey.F7,
                SDL.Keycode.F8 => ImGuiKey.F8,
                SDL.Keycode.F9 => ImGuiKey.F9,
                SDL.Keycode.F10 => ImGuiKey.F10,
                SDL.Keycode.F11 => ImGuiKey.F11,
                SDL.Keycode.F12 => ImGuiKey.F12,
                _ => ImGuiKey.None,
            };

            return imguiKey != ImGuiKey.None;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetClipboardTextDelegate(ImGuiContext* context, char* text);

        public void SetClipboardText(ImGuiContext* context, char* text)
        {
            var span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)text);
            var requestedText = Encoding.UTF8.GetString(span);
            SDL.SetClipboardText(requestedText);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate char* GetClipboardTextDelegate(ImGuiContext* context);

        public char* GetClipboardText(ImGuiContext* context)
        {
            if (_clipboardText != IntPtr.Zero)
                Marshal.ZeroFreeCoTaskMemUTF8(_clipboardText);

            if (SDL.HasClipboardText())
            {
                var clipboardText = SDL.GetClipboardText();
                _clipboardText = Marshal.StringToCoTaskMemUTF8(clipboardText);
            }

            return (char*)_clipboardText;
        }

        public void UpdateImGuiMouse(int button, bool down)
        {
            var io = ImGui.GetIO();

            if (button == SDL.ButtonLeft)
                io.MouseDown[0] = down;

            if (button == SDL.ButtonRight)
                io.MouseDown[1] = down;

            if (button == SDL.ButtonMiddle)
                io.MouseDown[2] = down;
        }

        public void UpdateImGuiKey(SDL.Keycode keycode, bool down)
        {
            var io = ImGui.GetIO();

            if (TryMapKeys(keycode, out var imguiKey))
            {
                io.AddKeyEvent(imguiKey, down);
            }
        }

        public void UpdateImguiInput(string input)
        {
            var io = ImGui.GetIO();
            io.AddInputCharactersUTF8(input);
        }

        public void UpdateImGuiKeyModifiers(SDL.Keymod keymod)
        {
            var io = ImGui.GetIO();

            io.AddKeyEvent(ImGuiKey.ModCtrl, (keymod & SDL.Keymod.Ctrl) != 0);
            io.AddKeyEvent(ImGuiKey.ModShift, (keymod & SDL.Keymod.Shift) != 0);
            io.AddKeyEvent(ImGuiKey.ModAlt, (keymod & SDL.Keymod.Alt) != 0);
            io.AddKeyEvent(ImGuiKey.ModSuper, (keymod & SDL.Keymod.GUI) != 0);
        }

        public void UpdateImGuiMouseMove(float x, float y)
        {
            var io = ImGui.GetIO();
            io.MousePos = new Vector2(x, y);
        }

        public void UpdateImGuiMouseWheel(float x, float y)
        {
            var io = ImGui.GetIO();
            io.MouseWheel = y;
            io.MouseWheelH = x;
        }

        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            SDL.GetWindowSize(_window, out var logicalWidth, out var localHeight);
            SDL.GetWindowSizeInPixels(_window, out var width, out var height);

            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(logicalWidth, localHeight);

            if (logicalWidth > 0 && localHeight > 0)
            {
                io.DisplayFramebufferScale = new Vector2((float)width / logicalWidth, (float)height / localHeight);
            }

            io.DeltaTime = deltaSeconds;
        }

        private void DrawImGui(SlPassContext ctx)
        {
            var drawData = ImGui.GetDrawData();
            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            var framebufferWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
            var framebufferHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);

            if (framebufferWidth <= 0 || framebufferHeight <= 0 || drawData.TotalVtxCount == 0)
                return;

            var vertices = ctx.Ring.Allocate((ulong)(drawData.TotalVtxCount * sizeof(ImDrawVert)));
            var indices = ctx.Ring.Allocate((ulong)(drawData.TotalIdxCount * sizeof(ushort)));

            var vtxDst = (ImDrawVert*)Unsafe.AsPointer(ref vertices.Data[0]);
            var idxDst = (ushort*)Unsafe.AsPointer(ref indices.Data[0]);

            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];

                Unsafe.CopyBlock(vtxDst, cmdList.VtxBuffer.Data, (uint)cmdList.VtxBuffer.Size * (uint)sizeof(ImDrawVert));
                Unsafe.CopyBlock(idxDst, cmdList.IdxBuffer.Data, (uint)cmdList.IdxBuffer.Size * sizeof(ushort));

                vtxDst += cmdList.VtxBuffer.Size;
                idxDst += cmdList.IdxBuffer.Size;
            }

            var io = ImGui.GetIO();
            var mvp = Matrix4x4.CreateOrthographicOffCenter(
                0f, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, -1.0f, 1.0f);

            ctx.SetPipeline(_material.Pipeline(in Variant, ctx.Signature));
            ctx.PushConstants(in mvp);
            ctx.SetVertexBuffer(0, vertices.View, (uint)sizeof(ImDrawVert));
            ctx.SetIndexBuffer(indices.View);
            ctx.SetViewport(0, 0, framebufferWidth, framebufferHeight);

            SDL.GetWindowSizeInPixels(_window, out var windowWidth, out var windowHeight);

            var vtxOffset = 0;
            var idxOffset = 0;

            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];

                for (var i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    var cmd = cmdList.CmdBuffer[i];

                    if (cmd.UserCallback != null)
                        continue;

                    Vector2 clipMin = new(cmd.ClipRect.X, cmd.ClipRect.Y);
                    Vector2 clipMax = new(cmd.ClipRect.Z, cmd.ClipRect.W);

                    if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                        continue;

                    var texturePush = new ImGuiTexturePush
                    {
                        TextureIndex = (uint)cmd.GetTexID(),
                        SamplerIndex = _fontSampler.Slot
                    };
                    ctx.PushConstants(in texturePush, ImGuiPushConstants.TextureOffset);

                    ctx.SetScissor(
                        (int)clipMin.X,
                        (int)clipMin.Y,
                        (int)Math.Clamp(clipMax.X, 0, windowWidth),
                        (int)Math.Clamp(clipMax.Y, 0, windowHeight));

                    ctx.DrawIndexed(cmd.ElemCount, 1,
                        (uint)(idxOffset + cmd.IdxOffset),
                        (int)(vtxOffset + cmd.VtxOffset));
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }

        public void Dispose()
        {
            foreach (var (texture, index) in _gpuTextures.Values)
            {
                _device.Tables.Release(index);
                _device.Retire(texture);
            }

            _gpuTextures.Clear();
            _material.Dispose();
        }
    }
}
