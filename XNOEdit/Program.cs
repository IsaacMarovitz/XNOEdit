using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Plume;
using SDL3;
using Solaris;
using Solaris.Graph;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.Render;
using XNOEdit.Services;
using LogLevel = XNOEdit.Logging.LogLevel;

namespace XNOEdit
{
    internal static class Program
    {
        public static event Action? GameFolderLoaded;

        private static IntPtr _window;
        private static SlDevice _device;
        private static SlFrameGraph _graph;
        private static SlUploader _uploader;

        private static RenderSettings _settings = new();
        private static UIManager UIManager;
        private static TextureManager _textureManager;
        private static InputManager _input;
        private static SceneView _view;
        private static SceneLoader _loader;

        private static ulong _previousTick;
        private static float _deltaTime;

        private static void Main(string[] args)
        {
            SDL.Init(SDL.InitFlags.Video);
            SDL.SetAppMetadata("XNOEdit", "1.0.0", "com.isaacmarovitz.xnoedit");

            // Don't use SDL.RunApp
            // it causes problems for the debugger
            if (AppInit(IntPtr.Zero, args.Length, args) != SDL.AppResult.Continue)
                return;

            while (true)
            {
                while (SDL.PollEvent(out var @event))
                {
                    var result = AppEvent(IntPtr.Zero, ref @event);
                    if (result != SDL.AppResult.Continue)
                    {
                        AppQuit(IntPtr.Zero, result);
                        return;
                    }
                }

                if (AppIter(IntPtr.Zero) != SDL.AppResult.Continue)
                    break;
            }

            AppQuit(IntPtr.Zero, SDL.AppResult.Success);
        }

        private static SDL.AppResult AppInit(IntPtr appState, int argc, string[] argv)
        {
            _window = SDL.CreateWindow("XNOEdit", 1280, 720,
                SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Resizable | SDL.WindowFlags.Metal);

            SDL.StartTextInput(_window);

            InitializeDevice();

            _textureManager = new TextureManager(_device);
            _view = new SceneView(_device, _textureManager);

            var imguiController = new ImGuiController(_device, _uploader, _window);
            UIManager = new UIManager();
            UIManager.OnLoad(imguiController, _device, _window);
            UIManager.ResetCameraAction += _view.ResetCamera;

            _input = new InputManager(_window, UIManager, _view, _settings);

            _loader = new SceneLoader(_device, _uploader, _window, UIManager, _view, _textureManager);
            UIManager.ObjectsPanel?.LoadObject += _loader.QueueObjectLoad;
            UIManager.StagesPanel?.LoadStage += _loader.QueueStageLoad;
            UIManager.MissionsPanel?.LoadMission += _loader.QueueMissionLoad;
            GameFolderLoaded += _loader.LoadGameFolderResources;

            Logger.SetEnable(LogLevel.Debug, Configuration.DebugLogs);

            if (Configuration.GameFolder != null)
            {
                _loader.LoadGameFolderResources();
            }

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppIter(IntPtr appState)
        {
            var diff = SDL.GetTicks() - _previousTick;
            _previousTick = SDL.GetTicks();
            _deltaTime = Math.Max((float)diff / 1000, 0.000001f);

            _input.Update(_deltaTime);
            _loader.ProcessMainThreadQueue();

            OnRender(_deltaTime);

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event @event)
        {
            switch (@event.Type)
            {
                case (uint)SDL.EventType.WindowResized:
                    var window = SDL.GetWindowFromEvent(@event);
                    SDL.GetWindowSizeInPixels(window, out var width, out var height);
                    OnFramebufferResize(new Vector2(width, height));
                    break;
                case (uint)SDL.EventType.DropFile:
                    if (Marshal.PtrToStringUTF8(@event.Drop.Data) is { } path)
                        OnFileDrop(path);
                    break;
                case (uint)SDL.EventType.Quit:
                case (uint)SDL.EventType.WindowCloseRequested:
                    return SDL.AppResult.Success;
                default:
                    _input.HandleEvent(in @event);
                    break;
            }

            return SDL.AppResult.Continue;
        }

        private static void InitializeDevice()
        {
            _device = SlDevice.Create();
            _graph = new SlFrameGraph(_device, SlSurface.FromSdlWindow(_window), RenderFormat.B8G8R8A8Unorm, maxFrameLatency: 2);
            _graph.SetVsyncEnabled(true);

            _uploader = new SlUploader(_device);

            Logger.Info?.PrintMsg(LogClass.Application, $"Solaris Backend: {_device.Backend} ({_device.Name})");
        }

        private static void OnRender(float deltaTime)
        {
            // Resize the viewport targets from last frame's ImGui layout, before anything
            // samples or renders to them.
            UIManager.ViewportPanel.PrepareFrame();

            var view = _view.Camera.GetViewMatrix();
            var projection = _view.Camera.GetProjectionMatrix(UIManager.ViewportPanel.GetAspectRatio());

            // Build the UI and finalise its draw data.
            UIManager.BuildUI(view, deltaTime, _settings, _view.Environment, _textureManager);
            ImGui.Render();

            // Servicing texture requests stages uploads, so both must complete before the
            // frame opens — a staged atlas is not readable until the flush lands.
            UIManager.Controller?.PrepareFrame();
            _uploader.Flush();

            using var frame = _graph.BeginFrame();

            if (frame == null)
            {
                return;
            }

            var viewportColor = frame.ImportTexture(UIManager.ViewportPanel.ColorTarget);
            var viewportDepth = frame.ImportTexture(UIManager.ViewportPanel.DepthTarget);
            var viewportResolve = frame.ImportTexture(UIManager.ViewportPanel.ResolveTarget);

            var scene = frame.AddPass("Scene")
                .Color(viewportColor, SlLoadOp.Clear, SlClearValue.Color(0.1f, 0.1f, 0.1f))
                .Depth(viewportDepth);

            if (UIManager.ViewportPanel.IsMultisampled)
            {
                scene.ResolveTo(viewportColor, viewportResolve);
            }

            scene.Execute(ctx => _view.Draw(ctx, view, projection, _settings));

            frame.AddPass("UI")
                .Reads(viewportResolve)
                .Color(frame.SwapChainTarget, SlLoadOp.Clear, SlClearValue.Color(0.15f, 0.15f, 0.15f))
                .Execute(ctx => UIManager.Controller?.Render(ctx));
        }

        private static void OnFramebufferResize(Vector2 size)
        {
            _graph.Resize();
        }

        private static void AppQuit(IntPtr appState, SDL.AppResult result)
        {
            _loader?.Cancel();

            _graph?.WaitForIdle();

            _uploader?.Dispose();
            _view?.Dispose();

            _textureManager?.Dispose();

            UIManager?.Dispose();

            _graph?.Dispose();
            _device?.Dispose();

            SDL.DestroyWindow(_window);
            SDL.Quit();
        }

        private static void OnFileDrop(string file)
        {
            if (!Directory.Exists(file)) return;

            var defaultXex = Path.Combine(file, "default.xex");
            if (File.Exists(defaultXex))
            {
                // We can reasonably assume this is the right folder
                Configuration.GameFolder = file;
                GameFolderLoaded?.Invoke();
            }
        }
    }
}
