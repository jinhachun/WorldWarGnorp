using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 「전투에서 승리할 때마다,」 이번 판에 등장하는 무작위 기물을 얻는다 (대지주 스팀바론 신화 — 기획서 §7-1).
    // 등급을 가리지 않고 풀에서 고르게 뽑는다. 얻는 길은 판도라의 상자와 같다(RunState.Acquire — 빈 칸이 없으면 못 얻고, 판매가는 0골드)
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Random Building On Win")]
    public class RandomBuildingOnWinPassive : BuildingPassive
    {
        [SuffixLabel("개", true)]
        [SerializeField] private int _count = 1;

        public override void OnBattleWon(OwnedBuilding self)
        {
            BuildingDefinition[] pool = RunState.Pool;
            for (int i = 0; i < _count; i++)
                RunState.Acquire(pool[Random.Range(0, pool.Length)]);
        }

        public override string Describe(int level) => $"전투에서 승리할 때마다, 무작위 기물 {_count}개를 얻습니다.";
    }
}
