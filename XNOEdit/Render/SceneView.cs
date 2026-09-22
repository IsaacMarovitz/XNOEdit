using System.Numerics;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Managers;
using XNOEdit.Render.Renderers;

namespace XNOEdit.Render
{
    public sealed class SceneView : IDisposable
    {
        private readonly SlDevice _device;
        private readonly TextureManager _textureManager;
        private readonly SkyboxRenderer _skybox;
        private readonly GuestDrawContext _guestDraw = new();

        private GridRenderer _grid;
        private Vector3 _center = Vector3.Zero;
        private float _radius = 1.0f;

        public SceneView(SlDevice device, TextureManager textureManager)
        {
            _device = device;
            _textureManager = textureManager;
            _grid = new GridRenderer(device);
            _skybox = new SkyboxRenderer(device);
        }

        public Camera Camera { get; } = new();

        public Scene? Scene { get; private set; }

        public SceneConfig? Config { get; set; }

        /// <summary>Replaces the current scene without moving the camera.</summary>
        public void SetScene(Scene scene)
        {
            Scene?.Dispose();
            Scene = scene;
        }

        /// <summary>Points the camera at a new subject and sizes the grid to it.</summary>
        public void Frame(Vector3 center, float radius)
        {
            _center = center;
            _radius = radius;

            Camera.SetModelRadius(radius);
            Camera.NearPlane = 0.01f;
            Camera.FarPlane = Math.Max(radius * 10.0f, 1000.0f);

            _grid.Dispose();
            _grid = new GridRenderer(_device, radius * 4.0f);

            ResetCamera();
        }

        public void ResetCamera()
        {
            var distance = Math.Max(_radius * 2.5f, 10.0f);
            Camera.FrameTarget(_center, distance);
        }

        public void Draw(SlPassContext ctx, Matrix4x4 view, Matrix4x4 projection, RenderSettings settings)
        {
            _skybox.Draw(ctx, view, projection,
                new SkyboxParameters
                {
                    CameraPosition = Camera.Position,
                    SunDirection = settings.SunDirection,
                    SunColor = settings.SunColor
                });

            if (settings.ShowGrid)
            {
                _grid.Draw(ctx, view, projection,
                    new GridParameters
                    {
                        Model = Matrix4x4.CreateTranslation(_center),
                        Position = Camera.Position,
                        FadeDistance = _radius * 5.0f
                    });
            }

            Scene?.Render(ctx, view, projection,
                new ModelParameters
                {
                    Position = Camera.Position,
                    CullBackfaces = settings.BackfaceCulling,
                    GuestDraw = _guestDraw,
                    TextureManager = _textureManager,
                    Scene = Config ?? new SceneConfig(),
                    EnvMap = new SlTextureIndex()
                });
        }

        public void Dispose()
        {
            Scene?.Dispose();
            _grid.Dispose();
            _skybox.Dispose();
        }
    }
}
