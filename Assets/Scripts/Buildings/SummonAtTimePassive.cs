using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GnorpWar
{
    // 용의 둥지(공용 전설 — 사용자 10/27): 전투마다 한 번, 전투 시작 후 N초에 이 기물의 층에서 유닛을 소환한다. 쿨다운 기물이 아니라 가속 · 쿨다운 효과 · 즉시 발동을 받지 않는다.
    // 신화 표시(_mythicMark — 이 기물의 능력 목록에 있으면)가 있으면 그 뒤로도 N초마다 다시 소환한다. 기물이 멈춘 뒤(데스매치 — BoardsStopped)에는 소환하지 않는다
    [CreateAssetMenu(menuName = "GnorpWar/Passives/Summon At Time")]
    public class SummonAtTimePassive : BuildingPassive
    {
        [SerializeField] private UnitDefinition _unit;
        [SuffixLabel("기", true)]
        [SerializeField] private int _count = 1;
        [Tooltip("전투 시작 후 소환하는 시각")]
        [SuffixLabel("초", true)]
        [SerializeField] private float _seconds = 60f;
        [Tooltip("신화 줄의 표시 — 이 기물의 능력 목록에 있으면 위 간격마다 다시 소환한다")]
        [SerializeField] private SummonAtTimeMythicPassive _mythicMark;

        public float Seconds => _seconds;
        public UnitDefinition SummonUnit => _unit;
        public int Count => _count;

        public override void OnBattleStart(BoardRunner board, int slot, int level)
        {
            OwnedBuilding self = board.Slots[slot].Building;
            bool repeat = _mythicMark != null && System.Array.IndexOf(self.Passives, _mythicMark) >= 0;
            board.StartCoroutine(SummonLater(board, slot, self, repeat));
        }

        private IEnumerator SummonLater(BoardRunner board, int slot, OwnedBuilding self, bool repeat)
        {
            float at = _seconds;
            // 전투 시작 훅(Begin)은 BattleManager가 전투 중으로 바뀌기 전에 불린다 — 바뀔 때까지 기다린다. 전투가 끝나면 씬과 함께 이 코루틴도 사라진다
            while (!BattleManager.Fighting)
                yield return null;
            while (BattleManager.Fighting && !BattleManager.BoardsStopped)
            {
                if (BattleManager.Elapsed >= at)
                {
                    SummonNow(board, slot, self);
                    if (!repeat)
                        yield break;
                    at += _seconds;
                }
                yield return null;
            }
        }

        // 지금 바로 한 번(시험 · 위의 시각) — 나온 자리에 기둥 · 칸 튐
        public void SummonNow(BoardRunner board, int slot, OwnedBuilding self)
        {
            board.Summon(_unit, _count, slot);
            Unit spawned = board.LastSpawned;
            if (spawned != null)
                PillarFx.Rise(new Vector2(spawned.Position.x, spawned.Feet), spawned.Top - spawned.Feet);
            OwnedBuilding.NotifyEffect(self);
        }

        public override string Describe(int level) => $"전투 {_seconds:0}초에 {_unit.DisplayName} {_count}기를 소환합니다.";
    }
}
