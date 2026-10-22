using System.IO;
using UnityEditor;

namespace GnorpWar.Editor
{
    // 개발 확인용 — 내 런 기록(Run/RunRecord)이 쌓이는 폴더를 탐색기로 연다
    internal static class RunRecordMenu
    {
        [MenuItem("GnorpWar/런 기록 폴더 열기")]
        private static void Open()
        {
            Directory.CreateDirectory(RunRecord.Folder);
            EditorUtility.OpenWithDefaultApp(RunRecord.Folder);
        }
    }
}
