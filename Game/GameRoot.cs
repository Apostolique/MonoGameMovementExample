using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Apos.Input;
using System.Text.Json.Serialization.Metadata;
using Apos.Camera;
using Apos.Shapes;
using MonoGame.Extended;
using FontStashSharp;

namespace GameProject {
    public class GameRoot : Game {
        public GameRoot() {
            _graphics = new GraphicsDeviceManager(this) {
                GraphicsProfile = GraphicsProfile.HiDef
            };
            IsMouseVisible = true;
            Content.RootDirectory = "Content";

            _settings = EnsureJson("Settings.json", SettingsContext.Default.Settings);
        }

        protected override void Initialize() {
            // TODO: Add your initialization logic here
            Window.AllowUserResizing = true;

            IsFixedTimeStep = _settings.IsFixedTimeStep;
            _graphics.SynchronizeWithVerticalRetrace = _settings.IsVSync;

            _settings.IsFullscreen = _settings.IsFullscreen || _settings.IsBorderless;

            RestoreWindow();
            if (_settings.IsFullscreen) {
                ApplyFullscreenChange(false);
            }

            _camera = new Camera(new DensityViewport(GraphicsDevice, Window, 500, 500));

            base.Initialize();
        }

        protected override void LoadContent() {
            _s = new SpriteBatch(GraphicsDevice);
            _sb = new ShapeBatch(GraphicsDevice, Content);

            // TODO: use this.Content to load your game content here
            InputHelper.Setup(this);

            _fontSystem = new FontSystem();
            _fontSystem.AddFont(TitleContainer.OpenStream($"{Content.RootDirectory}/source-code-pro-medium.ttf"));

            _background = Content.Load<Texture2D>("background");
            _infinite = Content.Load<Effect>("infinite");
        }

        protected override void UnloadContent() {
            if (!_settings.IsFullscreen) {
                SaveWindow();
            }

            SaveJson("Settings.json", _settings, SettingsContext.Default.Settings);

            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime) {
            _fps.Update(gameTime);
            InputHelper.UpdateSetup();

            if (_quit.Pressed())
                Exit();

            if (_toggleFullscreen.Pressed()) {
                ToggleFullscreen();
            }
            if (_toggleBorderless.Pressed()) {
                ToggleBorderless();
            }

            float move = (float)(0.1d * gameTime.ElapsedGameTime.TotalMilliseconds);

            if (_left.Held()) _character.X -= move;
            if (_up.Held()) _character.Y -= move;
            if (_right.Held()) _character.X += move;
            if (_down.Held()) _character.Y += move;

            _camera.XY = _character.Center;

            InputHelper.UpdateCleanup();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            _fps.Draw(gameTime);
            GraphicsDevice.Clear(TWColor.Gray700);

            Matrix uv_transform = GetUVTransform(_background, new Vector2(0, 0), 1f, _camera.VirtualViewport);

            _infinite.Parameters["view_projection"].SetValue(Matrix.Identity * _camera.GetProjection());
            _infinite.Parameters["uv_transform"].SetValue(Matrix.Invert(uv_transform));

            _s.Begin(effect: _infinite, samplerState: SamplerState.LinearWrap);
            _s.Draw(_background, GraphicsDevice.Viewport.Bounds, Color.White);
            _s.End();

            _sb.Begin(_camera.View);
            _sb.DrawRectangle(_character.Position, _character.Size, TWColor.Gray500, TWColor.Black, 2f);

            _sb.DrawRectangle(new Vector2(100f, 100f), new Vector2(50f, 50f), TWColor.Red600, TWColor.Black, 2f);
            _sb.DrawRectangle(new Vector2(200f, 100f), new Vector2(50f, 50f), TWColor.Blue600, TWColor.Black, 2f);
            _sb.End();

            var font = _fontSystem.GetFont(24);
            _s.Begin();
            _s.DrawString(font, $"FPS: {_fps.FramesPerSecond} -- Dropped: {_fps.DroppedFrames}", new Vector2(10, 10), Color.White);
            _s.End();

            base.Draw(gameTime);
        }

        private void ToggleFullscreen() {
            bool oldIsFullscreen = _settings.IsFullscreen;

            if (_settings.IsBorderless) {
                _settings.IsBorderless = false;
            } else {
                _settings.IsFullscreen = !_settings.IsFullscreen;
            }

            ApplyFullscreenChange(oldIsFullscreen);
        }
        private void ToggleBorderless() {
            bool oldIsFullscreen = _settings.IsFullscreen;

            _settings.IsBorderless = !_settings.IsBorderless;
            _settings.IsFullscreen = _settings.IsBorderless;

            ApplyFullscreenChange(oldIsFullscreen);
        }

        public static string GetPath(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        public static T LoadJson<T>(string name, JsonTypeInfo<T> typeInfo) where T : new() {
            T json;
            string jsonPath = GetPath(name);

            if (File.Exists(jsonPath)) {
                json = JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath), typeInfo);
            } else {
                json = new T();
            }

            return json;
        }
        public static void SaveJson<T>(string name, T json, JsonTypeInfo<T> typeInfo) {
            string jsonPath = GetPath(name);
            string jsonString = JsonSerializer.Serialize(json, typeInfo);
            File.WriteAllText(jsonPath, jsonString);
        }
        public static T EnsureJson<T>(string name, JsonTypeInfo<T> typeInfo) where T : new() {
            T json;
            string jsonPath = GetPath(name);

            if (File.Exists(jsonPath)) {
                json = JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath), typeInfo);
            } else {
                json = new T();
                string jsonString = JsonSerializer.Serialize(json, typeInfo);
                File.WriteAllText(jsonPath, jsonString);
            }

            return json;
        }

        private void ApplyFullscreenChange(bool oldIsFullscreen) {
            if (_settings.IsFullscreen) {
                if (oldIsFullscreen) {
                    ApplyHardwareMode();
                } else {
                    SetFullscreen();
                }
            } else {
                UnsetFullscreen();
            }
        }
        private void ApplyHardwareMode() {
            _graphics.HardwareModeSwitch = !_settings.IsBorderless;
            _graphics.ApplyChanges();
        }
        private void SetFullscreen() {
            SaveWindow();

            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.HardwareModeSwitch = !_settings.IsBorderless;

            _graphics.IsFullScreen = true;
            _graphics.ApplyChanges();
        }
        private void UnsetFullscreen() {
            _graphics.IsFullScreen = false;
            RestoreWindow();
        }
        private void SaveWindow() {
            _settings.X = Window.ClientBounds.X;
            _settings.Y = Window.ClientBounds.Y;
            _settings.Width = Window.ClientBounds.Width;
            _settings.Height = Window.ClientBounds.Height;
        }
        private void RestoreWindow() {
            Window.Position = new Point(_settings.X, _settings.Y);
            _graphics.PreferredBackBufferWidth = _settings.Width;
            _graphics.PreferredBackBufferHeight = _settings.Height;
            _graphics.ApplyChanges();
        }
        private Matrix GetUVTransform(Texture2D t, Vector2 offset, float scale, IVirtualViewport v) {
            return
                Matrix.CreateScale(t.Width, t.Height, 1f) *
                Matrix.CreateScale(scale, scale, 1f) *
                Matrix.CreateTranslation(offset.X, offset.Y, 0f) *
                _camera.View *
                Matrix.CreateScale(1f / v.Width, 1f / v.Height, 1f);
        }

        GraphicsDeviceManager _graphics;
        SpriteBatch _s;
        ShapeBatch _sb;

        FPSCounter _fps = new FPSCounter();

        Camera _camera;

        RectangleF _character = new RectangleF(0, 0, 50, 50);
        Texture2D _background;
        Effect _infinite;

        Settings _settings;
        FontSystem _fontSystem;

        ICondition _quit =
            new AnyCondition(
                new KeyboardCondition(Keys.Escape),
                new GamePadCondition(GamePadButton.Back, 0)
            );
        ICondition _toggleFullscreen =
            new AllCondition(
                new KeyboardCondition(Keys.LeftAlt),
                new KeyboardCondition(Keys.Enter)
            );
        ICondition _toggleBorderless = new KeyboardCondition(Keys.F11);
        ICondition _left = new KeyboardCondition(Keys.Left);
        ICondition _up = new KeyboardCondition(Keys.Up);
        ICondition _right = new KeyboardCondition(Keys.Right);
        ICondition _down = new KeyboardCondition(Keys.Down);

        private static JsonSerializerOptions _options = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }
}
