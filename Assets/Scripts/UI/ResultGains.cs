using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 결과 창 — 이번 승리로 넘어온 땅들에 붙은 지역 업그레이드를 아이콘 한 줄로(UI/UpgradeRow — 같은 종류는 「×N」, 올리면 툴팁).
    // 이 오브젝트의 자리가 줄의 가운데 위다. 얻은 땅이 없으면(패배) 아무것도 안 보인다
    public class ResultGains : MonoBehaviour
    {
        // 한 칸의 폭(px) — 여섯 종이 전부 「×N」이어도 한 줄
        private const float CellWidth = 80f;

        [Tooltip("「×N」 글의 본 — 도트 폰트 글 하나(같은 폰트 · 크기로 본뜬다)")]
        [SerializeField] private Text _labelTemplate;

        private UpgradeRow _row;

        // lands = 이번에 넘어온 땅들(RunState.LastGains)
        public void Show(IReadOnlyList<int> lands)
        {
            WorldMapDefinition map = RunState.World.Map;
            int kinds = System.Enum.GetValues(typeof(RegionUpgradeKind)).Length;
            var counts = new int[kinds];
            foreach (int land in lands)
                counts[(int)map.UpgradeOf(land).Kind]++;
            _row ??= new UpgradeRow((RectTransform)transform, _labelTemplate, CellWidth, kinds, false);
            _row.Show(map, kind => counts[(int)kind], "이번에 {0}개");
            // 보이는 칸들을 가운데로
            _row.Root.anchoredPosition = new Vector2(-_row.Width * 0.5f, 0f);
        }
    }
}
