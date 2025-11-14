using System.Drawing;
using System.Numerics;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using Pfim;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace XNOEdit
{
    class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static IKeyboard _primaryKeyboard;

        private static ImGuiController _controller;
        private static ImGuiXnoPanel _xnoPanel;
        private static IInputContext _input;
        private static readonly Dictionary<string, uint> Textures = [];

        private static Vector3 _cameraPosition = new(0.0f, 0.0f, 3.0f);
        private static Vector3 _cameraFront = new(0.0f, 0.0f, -1.0f);
        private static readonly Vector3 CameraUp = Vector3.UnitY;
        private static Vector3 _cameraDirection = Vector3.Zero;
        private static float _cameraYaw = -90f;
        private static float _cameraPitch = 0f;
        private static float _cameraZoom = 45f;

        private static Texture _texture;
        private static Shader _shader;
        // private static Model Model;

        private static Vector2 _lastMousePosition;

        static void Main(string[] args)
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(800, 600),
                Title = "XNOEdit"
            };

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Closing += OnClose;
            _window.FileDrop += OnFileDrop;

            _window.Run();
            _window.Dispose();
        }

        private static void OnLoad()
        {
            _input = _window.CreateInput();
            _primaryKeyboard = _input.Keyboards.FirstOrDefault();

            if (_primaryKeyboard != null)
            {
                _primaryKeyboard.KeyDown += KeyDown;
            }
            for (int i = 0; i < _input.Mice.Count; i++)
            {
                _input.Mice[i].Cursor.CursorMode = CursorMode.Raw;
                _input.Mice[i].MouseMove += OnMouseMove;
                _input.Mice[i].Scroll += OnMouseWheel;
            }

            _gl = _window.CreateOpenGL();

            _controller = new ImGuiController(_gl, _window, _input);

            _gl.ClearColor(Color.FromArgb(40, 40, 40));
        }

        private static void OnUpdate(double deltaTime)
        {
            var moveSpeed = 2.5f * (float) deltaTime;

            if (_primaryKeyboard.IsKeyPressed(Key.W))
            {
                _cameraPosition += moveSpeed * _cameraFront;
            }

            if (_primaryKeyboard.IsKeyPressed(Key.S))
            {
                _cameraPosition -= moveSpeed * _cameraFront;
            }

            if (_primaryKeyboard.IsKeyPressed(Key.A))
            {
                _cameraPosition -= Vector3.Normalize(Vector3.Cross(_cameraFront, CameraUp)) * moveSpeed;
            }

            if (_primaryKeyboard.IsKeyPressed(Key.D))
            {
                _cameraPosition += Vector3.Normalize(Vector3.Cross(_cameraFront, CameraUp)) * moveSpeed;
            }
        }

        private static void OnRender(double deltaTime)
        {
            _controller.Update((float) deltaTime);

            _gl.Enable(EnableCap.DepthTest);
            _gl.Clear(ClearBufferMask.ColorBufferBit |  ClearBufferMask.DepthBufferBit);

            var size = _window.FramebufferSize;

            var view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraPosition + _cameraFront, CameraUp);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(DegreesToRadians(_cameraZoom), (float)size.X / size.Y, 0.1f, 100.0f);

            if (_xnoPanel != null)
            {
                _xnoPanel.Render(Textures);
            }

            _controller.Render();
        }

        private static void OnFramebufferResize(Vector2D<int> size)
        {
            _gl.Viewport(size);
        }

        private static unsafe void OnMouseMove(IMouse mouse, Vector2 position)
        {
            var lookSensitivity = 0.1f;
            if (_lastMousePosition == default)
            {
                _lastMousePosition = position;
            }
            else
            {
                var xOffset = (position.X - _lastMousePosition.X) * lookSensitivity;
                var yOffset = (position.Y - _lastMousePosition.Y) * lookSensitivity;
                _lastMousePosition = position;

                _cameraYaw += xOffset;
                _cameraPitch -= yOffset;

                _cameraPitch = Math.Clamp(_cameraPitch, -89.0f, 89.0f);

                _cameraDirection.X = MathF.Cos(DegreesToRadians(_cameraYaw)) * MathF.Cos(DegreesToRadians(_cameraPitch));
                _cameraDirection.Y = MathF.Sin(DegreesToRadians(_cameraPitch));
                _cameraDirection.Z = MathF.Sin(DegreesToRadians(_cameraYaw)) * MathF.Cos(DegreesToRadians(_cameraPitch));
                _cameraFront = Vector3.Normalize(_cameraDirection);
            }
        }

        private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            _cameraZoom = Math.Clamp(_cameraZoom - scrollWheel.Y, 1.0f, 45f);
        }

        private static void OnClose()
        {
            _controller.Dispose();
            _input.Dispose();

            foreach (var texture in Textures)
            {
                _gl.DeleteTexture(texture.Value);
            }

            _gl.Dispose();
        }

        private static unsafe void OnFileDrop(string[] files)
        {
            foreach (var file in files)
            {
                var xno = new NinjaNext(file);
                _xnoPanel = new ImGuiXnoPanel(xno);

                Console.WriteLine($"Loaded XNO {xno.Name}");

                foreach (var texture in Textures)
                {
                    _gl.DeleteTexture(texture.Value);
                }

                Textures.Clear();

                var textureListChunk = xno.GetChunk<TextureListChunk>();

                foreach (var texture in textureListChunk.Textures)
                {
                    var folderPath = Path.GetDirectoryName(file);
                    var texturePath = Path.Combine(folderPath, texture.Name);

                    using var image = Pfimage.FromFile(texturePath);

                    var glTexture = _gl.GenTexture();
                    _gl.ActiveTexture(TextureUnit.Texture0);
                    _gl.BindTexture(TextureTarget.Texture2D, glTexture);

                    fixed (byte* ptr = image.Data)
                    {
                        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint) image.Width,
                            (uint) image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                    }

                    var glMinFilter = texture.MinFilter switch
                    {
                        MinFilter.NND_MIN_NEAREST => (int)TextureMinFilter.Nearest,
                        MinFilter.NND_MIN_LINEAR => (int)TextureMinFilter.Linear,
                        MinFilter.NND_MIN_NEAREST_MIPMAP_NEAREST => (int)TextureMinFilter.NearestMipmapNearest,
                        MinFilter.NND_MIN_NEAREST_MIPMAP_LINEAR => (int)TextureMinFilter.NearestMipmapLinear,
                        MinFilter.NND_MIN_LINEAR_MIPMAP_NEAREST => (int)TextureMinFilter.LinearMipmapNearest,
                        MinFilter.NND_MIN_LINEAR_MIPMAP_LINEAR => (int)TextureMinFilter.LinearMipmapLinear,
                        _ => (int)TextureMinFilter.Linear
                    };

                    var glMagFilter = texture.MagFilter switch
                    {
                        MagFilter.NND_MAG_NEAREST => (int)TextureMinFilter.Nearest,
                        MagFilter.NND_MAG_LINEAR => (int)TextureMinFilter.Linear,
                        _ => (int)TextureMinFilter.Linear
                    };

                    _gl.GenerateMipmap(TextureTarget.Texture2D);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, glMinFilter);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, glMagFilter);

                    _gl.BindTexture(TextureTarget.Texture2D, 0);

                    Textures.Add(texture.Name, glTexture);
                }
            }
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int keyCode)
        {
            if (key == Key.Escape)
                _window.Close();
        }

        private static float DegreesToRadians(float degrees)
        {
            return MathF.PI / 180f * degrees;
        }
    }
}
