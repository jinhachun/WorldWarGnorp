using UnityEngine;

namespace GnorpWar
{
    // 용의 둥지 신화 줄 — 효과는 SummonAtTimePassive가 이 표시를 보고 낸다(그 간격마다 다시 소환). 글의 수치는 그 능력(_main)에서 읽는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon At Time Mythic")]
    public class SummonAtTimeMythicPassive : BuildingPassive
    {
        [SerializeField] private SummonAtTimePassive _main;

        public override string Describe(int level) => $"{_main.Seconds:0}초마다 {_main.SummonUnit.DisplayName} {_main.Count}기를 추가로 소환합니다.";
    }
}
