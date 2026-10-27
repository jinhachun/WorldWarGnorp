using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GnorpWar.Editor.Tests
{
    // 연계는 한 번만(사용자 10/26) — 연계로 태어난 유닛(Unit.ChainBorn)의 죽음 · 처치는 「죽으면 · 처치하면 소환」을 다시 부르지 않는다(예외: 까마귀의사는 적을 전부 센다 · 하마 교련장 신화는 자기가 낸 연계 병사의 처치를 센다).
    // 화면 없는 EditMode라 땅(Ground)도 보드(BoardRunner)도 없다 — 그래서 효과가 소환 단계까지 가면 거기서 예외로 멈춘다.
    // 「예외 없음」 = 소환 전에 건너뛰었다 · 「예외」 = 건너뛰지 않고 소환하러 갔다. 실제 전투에서의 동작은 폭주 시험(Editor/RunawayBattleTest)이 본다
    public class ChainBornTests
    {
        private static T Effect<T>(string name) where T : UnitEffect
        {
            var effect = AssetDatabase.LoadAssetAtPath<T>($"Assets/Data/Effects/{name}.asset");
            Assert.NotNull(effect, name);
            return effect;
        }

        private static Unit Prefab(string unit)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{unit}.asset");
            Assert.NotNull(definition, unit);
            return definition.PrefabFor(Team.Ally);
        }

        // 프리팹 그대로 표시만 바꾼다(씬에 꺼내지 않는다) — 끝나면 되돌린다
        private static void With(Unit unit, bool chained, Action body)
        {
            unit.NotifySummoned(-1, null, chained);
            try
            {
                body();
            }
            finally
            {
                unit.NotifySummoned(-1, null, false);
            }
        }

        [SetUp]
        public void Empty()
        {
            Assume.That(Ground.Instance == null && BoardRunner.For(Team.Ally) == null, "전투 밖(EditMode)에서만 — 땅 · 보드가 있으면 진짜 소환해 버린다");
            TeamEffects.Clear();
        }

        [Test]
        public void DeathSummon_SkipsChainBorn()   // 하마 교련장: 기사가 죽으면 병사
        {
            var effect = Effect<DeathSummonEffect>("Effect_Dismount");
            Unit knight = Prefab("Unit_Knight");
            With(knight, true, () => Assert.DoesNotThrow(() => effect.OnDied(knight, Vector2.zero, 0)));
            With(knight, false, () => Assert.Catch(() => effect.OnDied(knight, Vector2.zero, 0)));
        }

        [Test]
        public void DeathSummonChance_SkipsChainBorn()   // 거대 괴수의 무덤 신화: 중기병이 죽으면 100% 전사 5기
        {
            var effect = Effect<DeathSummonChanceEffect>("Effect_TribeBeastGraveMythic");
            Unit beast = Prefab("Unit_TribeWarBeast");
            With(beast, true, () => Assert.DoesNotThrow(() => effect.OnDied(beast, Vector2.zero, 0)));
            With(beast, false, () => Assert.Catch(() => effect.OnDied(beast, Vector2.zero, 0)));
        }

        [Test]
        public void EnemyDeathSummon_CountsChainBornVictim()   // 까마귀의사 신화: 적이 죽으면 병사 — 적은 연계로 태어났어도 센다(사용자 10/26)
        {
            var effect = Effect<EnemyDeathSummonEffect>("Effect_CrowDoctorMythic");
            Unit victim = Prefab("Unit_Melee");
            With(victim, true, () => Assert.Catch(() => effect.OnEnemyDied(Team.Enemy, victim, Vector2.zero, 0)));
            With(victim, false, () => Assert.Catch(() => effect.OnEnemyDied(Team.Enemy, victim, Vector2.zero, 0)));
        }

        [Test]
        public void KillSummon_SkipsChainBorn()   // 왕족친위대: 기사가 처치하면 친위기사 — 예외 없음(사용자 10/28: 친위기사는 기사가 아니라 발동 못 한다)
        {
            var effect = Effect<KillSummonEffect>("Effect_RoyalGuard");
            Unit killer = Prefab("Unit_Knight");
            Unit victim = Prefab("Unit_Melee");
            With(killer, true, () => Assert.DoesNotThrow(() => effect.OnKill(killer, victim, 0)));
            With(killer, false, () => Assert.Catch(() => effect.OnKill(killer, victim, 0)));
        }
    }
}
