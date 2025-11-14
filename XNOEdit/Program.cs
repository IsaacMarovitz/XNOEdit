using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using Pfim;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using XNOEdit.Renderer;

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

        private static Camera _camera;
        private static Renderer.Shader _shader;
        private static Model _model;
        private static GridRenderer _grid;
        private static SkyboxRenderer _skybox;
        private static bool _wireframeMode;
        private static bool _showGrid = true;
        private static bool _showAxes = true;
        private static bool _backfaceCulling;
        private static bool _mouseCaptured;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static float _modelRadius = 1.0f;
        private static Vector3 _sunDirection = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0.5f));

        private static Vector2 _lastMousePosition;

        static void Main(string[] args)
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
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

            foreach (var mouse in _input.Mice)
            {
                mouse.Cursor.CursorMode = CursorMode.Normal; // Changed from Raw
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseWheel;
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
            }

            _gl = _window.CreateOpenGL();
            _controller = new ImGuiController(_gl, _window, _input);
            _camera = new Camera();

            // Create shader and grid
            _shader = new Renderer.Shader(_gl, ShaderSources.VertexShader, ShaderSources.FragmentShader);
            _grid = new GridRenderer(_gl, 100.0f);
            _skybox = new SkyboxRenderer(_gl);

            // Sky blue background
            _gl.ClearColor(0.53f, 0.81f, 0.92f, 1.0f);
            _gl.Enable(EnableCap.DepthTest);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        private static void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (button == MouseButton.Right)
            {
                _mouseCaptured = true;
                mouse.Cursor.CursorMode = CursorMode.Raw;
                _lastMousePosition = default;
            }
        }

        private static void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button == MouseButton.Right)
            {
                _mouseCaptured = false;
                mouse.Cursor.CursorMode = CursorMode.Normal;
            }
        }

        private static void OnUpdate(double deltaTime)
        {
            _camera.ProcessKeyboard(_primaryKeyboard, (float)deltaTime);
        }

        private static void OnRender(double deltaTime)
        {
            _controller.Update((float) deltaTime);

            _gl.Enable(EnableCap.DepthTest);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var size = _window.FramebufferSize;
            var view = _camera.GetViewMatrix();
            var projection = _camera.GetProjectionMatrix((float)size.X / size.Y);

            _gl.Disable(EnableCap.CullFace);
            _skybox.Draw(view, projection, _sunDirection);

            if (_showGrid)
            {
                var gridModel = Matrix4x4.CreateTranslation(_modelCenter);
                float fadeDistance = _modelRadius * 5.0f;
                _grid.Draw(view, projection, gridModel, _camera.Transform.Position, fadeDistance);
            }

            _gl.PolygonMode(GLEnum.FrontAndBack, _wireframeMode ? GLEnum.Line : GLEnum.Fill);
            _gl.FrontFace(GLEnum.CW);

            if (_backfaceCulling)
            {
                _gl.Enable(EnableCap.CullFace);
                _gl.CullFace(GLEnum.Back);
            }
            else
            {
                _gl.Disable(EnableCap.CullFace);
            }

            // Render the model
            if (_model != null && _shader != null)
            {
                _shader.Use();
                _shader.SetUniform("uModel", Matrix4x4.Identity);
                _shader.SetUniform("uView", view);
                _shader.SetUniform("uProjection", projection);
                _shader.SetUniform("uLightDir", new Vector3(0.5f, -1.0f, 0.5f));
                _shader.SetUniform("uViewPos", _camera.Transform.Position);
                _shader.SetUniform("uLightColor", new Vector3(1.0f, 1.0f, 1.0f));

                _model.Draw(_shader);
            }

            // Reset to fill mode for UI
            _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);

            // Render UI
            if (_xnoPanel != null)
            {
                _xnoPanel.Render(Textures);
            }
            else
            {
                ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Text("Drag and drop a .xno file");
                ImGui.End();
            }

            _controller.Render();
        }

        private static void OnFramebufferResize(Vector2D<int> size)
        {
            _gl.Viewport(size);
        }

        private static void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (!_mouseCaptured)
                return;

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

                _camera.ProcessMouseMove(xOffset, yOffset);
            }
        }

        private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            _camera.ProcessMouseScroll(scrollWheel.Y);
        }

        private static void OnClose()
        {
            _model?.Dispose();
            _shader?.Dispose();
            _grid?.Dispose();
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
                try
                {
                    var xno = new NinjaNext(file);
                    _xnoPanel = new ImGuiXnoPanel(xno);

                    foreach (var texture in Textures)
                    {
                        _gl.DeleteTexture(texture.Value);
                    }
                    Textures.Clear();

                    // Load textures
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

                    // Load and create model
                    _model?.Dispose();
                    var objectChunk = xno.GetChunk<ObjectChunk>();
                    if (objectChunk != null)
                    {
                        // In OnFileDrop, after loading ObjectChunk:
                        var vertexListUsage = new Dictionary<int, int>();
                        foreach (var subObj in objectChunk.SubObjects)
                        {
                            foreach (var meshSet in subObj.MeshSets)
                            {
                                if (!vertexListUsage.ContainsKey(meshSet.VertexListIndex))
                                    vertexListUsage[meshSet.VertexListIndex] = 0;
                                vertexListUsage[meshSet.VertexListIndex]++;
                            }
                        }

                        var totalMeshes = objectChunk.SubObjects.Sum(sub => sub.MeshSets.Count);
                        Console.WriteLine($"Found {objectChunk.SubObjects.Count} sub-objects with {totalMeshes} total mesh sets");

                        _model = new Model(_gl, objectChunk);

                        // Use object-level bounding info for camera positioning
                        _modelCenter = objectChunk.Centre;
                        _modelRadius = objectChunk.Radius;

                        _camera.NearPlane = Math.Max(_modelRadius * 0.01f, 0.1f);
                        _camera.FarPlane = Math.Max(_modelRadius * 10.0f, 1000.0f);

                        _grid?.Dispose();
                        var gridSize = _modelRadius * 4.0f;
                        _grid = new GridRenderer(_gl, gridSize);

                        ResetCamera();
                    }
                    else
                    {
                        Console.WriteLine("Error: No ObjectChunk found in file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading file: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private static void ResetCamera()
        {
            var distance = Math.Max(_modelRadius * 2.5f, 10.0f);
            _camera.FrameTarget(_modelCenter, distance);
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int keyCode)
        {
            if (key == Key.Escape)
                _window.Close();

            // Toggle wireframe mode with F key
            if (key == Key.F)
            {
                _wireframeMode = !_wireframeMode;
                Console.WriteLine($"Wireframe mode: {(_wireframeMode ? "ON" : "OFF")}");
            }

            // Toggle grid with G key
            if (key == Key.G)
            {
                _showGrid = !_showGrid;
                Console.WriteLine($"Grid: {(_showGrid ? "ON" : "OFF")}");
            }

            // Toggle axes with X key
            if (key == Key.X)
            {
                _showAxes = !_showAxes;
                Console.WriteLine($"Axes: {(_showAxes ? "ON" : "OFF")}");
            }

            // Toggle backface culling with C key
            if (key == Key.C)
            {
                _backfaceCulling = !_backfaceCulling;
                Console.WriteLine($"Backface culling: {(_backfaceCulling ? "ON" : "OFF")}");
            }

            // Reset camera with R key
            if (key == Key.R)
            {
                ResetCamera();
                Console.WriteLine("Camera reset");
            }
        }

        private static float DegreesToRadians(float degrees)
        {
            return MathF.PI / 180f * degrees;
        }
    }
}
