using UnityEngine;

namespace GnorpWar
{
    // 글만 있는 능력 — 효과는 이 표시를 읽는 다른 능력이 낸다(약탈족장 라이칸스로프 신화: FireRandomAction이 이 표시가 있으면 고른 기물을 2번 발동시킨다)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Note")]
    public class NotePassive : BuildingPassive
    {
        [TextArea]
        [SerializeField] private string _text = "";

        public override string Describe(int level) => _text;
    }
}
