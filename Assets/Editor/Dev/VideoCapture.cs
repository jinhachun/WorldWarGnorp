using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 플레이 중 게임 화면을 MP4로 녹화한다(Unity Recorder) — Claude가 MCP로 연출(손맛)을 영상으로 확인 · 사용자에게 보내기 위함. 메뉴 없음.
    // VideoCapture.Start("C:/…/이름") → 플레이 → VideoCapture.Stop(). 확장자는 Recorder가 붙인다. 30fps 고정 · 소리 끔.
    // 컨트롤러를 static으로 들고 있어 execute_code 호출 사이에도 이어진다(도메인 리로드가 나면 녹화도 끊긴다)
    public static class VideoCapture
    {
        private static RecorderController _controller;
        // Recorder는 게임 창 크기를 「Recording Resolution」으로 바꾸고 되돌리지 않는다 — 녹화 전 크기 칸을 기억했다가 Stop에서 되돌린다
        private static int _savedSizeIndex = -1;
        private static readonly System.Type GameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");

        private static System.Reflection.PropertyInfo SizeIndex
            => GameViewType?.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static EditorWindow GameView()
        {
            Object[] views = GameViewType != null ? Resources.FindObjectsOfTypeAll(GameViewType) : new Object[0];
            return views.Length > 0 ? (EditorWindow)views[0] : null;
        }

        public static bool IsRecording => _controller != null && _controller.IsRecording();

        public static string Start(string pathWithoutExtension, int width = 960, int height = 540, float fps = 30f)
        {
            if (!Application.isPlaying)
                return "플레이 중에만";
            if (IsRecording)
                return "이미 녹화 중";
            EditorWindow view = GameView();
            _savedSizeIndex = view != null && SizeIndex != null ? (int)SizeIndex.GetValue(view) : -1;
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "VideoCapture";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.CaptureAudio = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            movie.OutputFile = pathWithoutExtension;
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = fps;
            settings.CapFrameRate = true;
            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            return _controller.StartRecording() ? "녹화 시작 " + pathWithoutExtension : "시작 실패";
        }

        public static string Stop()
        {
            if (_controller == null)
                return "녹화 없음";
            _controller.StopRecording();
            _controller = null;
            EditorWindow view = GameView();
            if (view != null && SizeIndex != null && _savedSizeIndex >= 0)
            {
                SizeIndex.SetValue(view, _savedSizeIndex);
                view.Repaint();
            }
            return "녹화 끝";
        }
    }
}
