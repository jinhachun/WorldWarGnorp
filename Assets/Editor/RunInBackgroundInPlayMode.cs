using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor
{
    // 에디터가 뒤에 있어도 플레이모드가 멈추지 않게 한다 — Claude가 MCP로 플레이 검증을 하기 위함.
    // 에디터에서만 돈다. 빌드의 동작(PlayerSettings.runInBackground)은 건드리지 않는다.
    [InitializeOnLoad]
    internal static class RunInBackgroundInPlayMode
    {
        static RunInBackgroundInPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    Application.runInBackground = true;
            };
        }
    }
}
