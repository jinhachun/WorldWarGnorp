using UnityEngine;
using UnityEngine.UI;

namespace GnorpWar
{
    // 전투 화면 — 본진 체력바 아래에 그 진영 영주가 가진 합치기 보상 업그레이드 아이콘 줄(UI/UpgradeRow). 내 것은 왼쪽부터, 적의 것은 오른쪽부터 서로 마주 보게.
    // 체력바의 자식이라 상점 동안에는 체력바와 같이 숨는다. 전투 한 판 동안 가진 업그레이드가 바뀌지 않으므로 처음 한 번만 그린다
    public class BaseUpgradeRow : MonoBehaviour
    {
        // 한 칸의 폭(px) · 한 줄의 칸 수 — 한 줄이 가운데의 유닛 수 표시에 닿지 않게. 넘치면 아랫줄
        private const float CellWidth = 80f;
        private const int Columns = 6;

        [SerializeField] private Team _team;
        [Tooltip("「×N」 글의 본 — 도트 폰트 글 하나(같은 폰트 · 크기로 본뜬다)")]
        [SerializeField] private Text _labelTemplate;

        private void Start()
        {
            Lord lord = _team == Team.Ally ? RunState.World.State.Player : RunState.Opponent;
            if (lord == null)
                return;
            new UpgradeRow((RectTransform)transform, _labelTemplate, CellWidth, Columns, _team == Team.Enemy).Show(RunState.UpgradesOf(lord));
        }
    }
}
