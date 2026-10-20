namespace GnorpWar
{
    // 받은 피해를 공격한 쪽에 그대로 돌려준다 — 무적으로 막은 피해(공주 루니카 신화)가 쓰고, 타워가 받은 피해(성채 신화)도 여기를 쓴다.
    // 반사 피해가 다시 반사를 부르지 않게 재진입을 막는다. 피해는 한 물리 스텝 안에서 동기로 끝나고 finally가 늘 되돌리므로 라운드마다 비울 것이 없다
    public static class DamageReflect
    {
        private static bool _reflecting;

        // hitDirection = 들어온 공격의 방향(공격자 → 피격자) — 돌려주는 피해는 그 반대로 간다. 밀림은 없다(push 0)
        public static void Back(IDamageable attacker, float amount, UnityEngine.Vector2 hitDirection, IDamageable from)
        {
            if (_reflecting || amount <= 0f || attacker == null || !attacker.IsAlive)
                return;
            _reflecting = true;
            try
            {
                attacker.TakeDamage(amount, -hitDirection, 0f, from);
                // 돌려받은 쪽에 보라 섬광(디버프 색) — 반사인 게 보이게(성채 신화 · 루니카 신화 · 가시 갑옷)
                if (FxDirector.Instance != null && (attacker is Unit || attacker is Base))
                {
                    UnityEngine.Vector2 at = attacker is Unit unit ? unit.Position : ((Base)attacker).Muzzle;
                    FxDirector.Instance.HitSpark(at, -hitDirection, BuffLinkFx.ColorOf(BuffKind.Debuff));
                }
            }
            finally
            {
                _reflecting = false;
            }
        }
    }
}
