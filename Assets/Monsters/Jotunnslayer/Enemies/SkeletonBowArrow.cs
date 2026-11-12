using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class SkeletonBowArrow : MonoBehaviour
    {
        const float Speed = 6f;
        const float Lifetime = 3f;
        const float HitRadius = .5f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock arrowColor;

        DraugArrowAttack draugArrow;
        internal void ConfigureDraug(DraugArrowAttack presentation)
        {
            draugArrow=presentation;
            name="Draug base arrow";transform.localScale=Vector3.one;
            GetComponent<MeshRenderer>().enabled=false;
            speed=6f;remaining=3f;hitRadius=.5f;
            pierces=piercesTerrain=false;reflectable=true;
        }
        CharacterInstance source;
        CharacterInstance target;
        Vector3 direction;
        float remaining;
        float speed;
        float hitRadius;
        int damage;
        bool pierces;
        bool piercesTerrain;
        bool splits;
        bool hitTarget;
        bool gravitates;
        bool reflectable;
        bool hybBossMissile;
        bool stoppedHoming;
        bool goblinReturningProjectile;
        int goblinPhase; // 0 outbound, 1 hold, 2 return
        float goblinPhaseTime;
        float goblinReturnTimeout;
        bool goblinLegHit;
        internal bool GoblinReturning => goblinReturningProjectile && goblinPhase==2;
        GoblinMagicVisual goblinMagic;
        internal void ConfigureGoblinReturn(GoblinMagicVisual magic)
        {
            goblinReturningProjectile = true;
            goblinMagic=magic;
            transform.localScale=Vector3.one;
            if(magic)
            {
                GetComponent<MeshRenderer>().enabled=false;
                magic.Release(transform);
            }
            goblinPhaseTime = 1.2f;
            goblinReturnTimeout = 10f; // Cleanup safeguard when caster continuously flees.
            speed = 7f;
            hitRadius = .5f;
            name = "Goblin returning projectile";
        }
        void TickGoblinReturn(float dt)
        {
            if(source.CurrentHealth <= 0) { Despawn(); return; }
            Vector3 start=transform.position;
            if(goblinPhase == 1)
            {
                if(!float.IsPositiveInfinity(SkillBattlePreview.TimeTrapProjectileContact(source,start,direction,0f,Time.time)))
                {Despawn();return;}
                goblinPhaseTime -= dt;
                if(goblinPhaseTime <= 0f)
                {
                    goblinPhase = 2;
                    goblinLegHit = false;
                    Debug.Log("GOBLIN_PROJECTILE_RETURN_BEGIN");
                }
                return;
            }
            float step=speed*dt;
            bool reachedHome=false;
            if(goblinPhase == 2)
            {
                goblinReturnTimeout-=dt;
                if(goblinReturnTimeout<=0f){Despawn();return;}
                Vector3 home=(goblinMagic?goblinMagic.ReturnPoint:source.CombatAimPoint)-start;
                reachedHome=home.magnitude<=step;
                step=Mathf.Min(step,home.magnitude);
                if(home.sqrMagnitude>0.000001f)
                {direction=home.normalized;transform.rotation=Quaternion.LookRotation(direction);}
            }
            else {step=speed*Mathf.Min(dt,goblinPhaseTime);goblinPhaseTime-=dt;}
            float trapContact=SkillBattlePreview.TimeTrapProjectileContact(source,start,direction,step,Time.time);
            float targetContact=float.PositiveInfinity;
            if(!goblinLegHit && target && target.CurrentHealth>0 && target.Team!=source.Team)
            {
                Vector3 offset=target.CombatAimPoint-start;
                float projection=Vector3.Dot(offset,direction);
                float lateralSquared=(offset-direction*projection).sqrMagnitude;
                if(lateralSquared<=hitRadius*hitRadius)
                {
                    float chord=Mathf.Sqrt(hitRadius*hitRadius-lateralSquared);
                    if(projection+chord>=0f && projection-chord<=step)
                        targetContact=Mathf.Max(0f,projection-chord);
                }
            }
            if(!float.IsPositiveInfinity(trapContact) && trapContact<=targetContact)
            {Despawn();return;}
            bool hit=!float.IsPositiveInfinity(targetContact);
            if(hit)
            {
                Vector3 contact=start+direction*targetContact;
                if(reflectable && SkillBattlePreview.TryReflectRaidProjectile(target,source))
                {
                    Reflect(contact);
                    // Reflection transfers damage attribution. It must not home to the old FX owner.
                    goblinReturningProjectile=false;
                    piercesTerrain=true;
                    remaining=Vector3.Distance(contact,target.CombatAimPoint)/speed+.1f;
                    Debug.Log("GOBLIN_PROJECTILE_REFLECTED");
                    return;
                }
                goblinLegHit=true;
                if(goblinMagic)goblinMagic.Impact(contact);
                target.Server_ChangeHealthStateRaw(damage,CharacterPart.body,AttackType.hitscan,source,0);
                Debug.Log($"GOBLIN_PROJECTILE_HIT leg={goblinPhase}");
                if(goblinPhase==0)
                {
                    // A non-piercing outbound hit stops before any later contact on this step.
                    transform.position=contact;goblinPhase=1;goblinPhaseTime=1f;
                    Debug.Log("GOBLIN_PROJECTILE_HOLD_BEGIN");return;
                }
            }
            if(!float.IsPositiveInfinity(trapContact)){Despawn();return;}
            transform.position=start+direction*step;
            if(reachedHome)
            {
                if(goblinMagic)goblinMagic.Impact(transform.position);
                Debug.Log("GOBLIN_PROJECTILE_END returned=True");
                Despawn();return;
            }
            if(goblinPhase==0 && (hit || goblinPhaseTime<=0f))
            {
                goblinPhase=1;goblinPhaseTime=1f;
                Debug.Log("GOBLIN_PROJECTILE_HOLD_BEGIN");
            }
        }
        const float MilipedeFlightTime = 1.3f;
        bool milipedeSpit;
        bool spiderSplash;
        internal const float SpiderSplashFlightTime = 1.4f;
        internal const float SpiderSplashRadius = 1f;
        static readonly AnimationCurve SpiderArc = new AnimationCurve(
            new Keyframe(0f, 0f, 5.027518f, 5.027518f),
            new Keyframe(.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -5.037392f, -5.037392f));
        Vector3 arcOrigin;
        Vector3 arcDestination;
        internal Vector3 MilipedeLanding => arcDestination;

        internal bool IsHybBossMissileFrom(CharacterInstance caster) => hybBossMissile && source == caster;

        internal static SkeletonBowArrow Launch(CharacterInstance source, CharacterInstance target, Vector3 direction, int damage, bool spell = false)
        {
            var flight = Create(source, target, source.CombatAimPoint, direction, damage, spell);
            flight.speed = spell ? 6.7f : Speed;
            flight.remaining = spell ? 5f : Lifetime;
            flight.hitRadius = spell ? .3f : HitRadius;
            if (!spell) flight.SetArrowReadability(new Color(1f, .7f, .16f));            else
            {
                // Keep the placeholder spell readable without changing its collision radius.
                flight.transform.localScale = Vector3.one * .45f;
                arrowColor ??= new MaterialPropertyBlock();
                arrowColor.SetColor(BaseColor, new Color(.55f, 1f, .12f));
                flight.GetComponent<MeshRenderer>().SetPropertyBlock(arrowColor);
            }
            flight.reflectable = true;
            return flight;
        }

        internal static SkeletonBowArrow LaunchElementalPoisonOrb(CharacterInstance source, CharacterInstance target, int damage)
        {
            Vector3 origin = source.CombatAimPoint;
            var orb = Create(source, target, origin, (target.CombatAimPoint - origin).normalized, damage, spell: true);
            orb.name = "Elemental poison orb";
            orb.speed = 3.5f;
            orb.remaining = 5.5f;
            orb.hitRadius = .6f;
            orb.pierces = true;
            orb.gravitates = true;
            orb.elementalPoisonOrb = true;
            return orb;
        }

        bool elementalPoisonOrb;
        internal void CancelElementalPoisonOrb() => Despawn();

        internal static SkeletonBowArrow LaunchMageFire(CharacterInstance source, CharacterInstance target,
            Vector3 direction, int damage)
        {
            var fire = Create(source, target, source.transform.position + Vector3.up, direction, damage, spell: true);
            fire.name = "Elemental mage fire ring";
            fire.speed = 8f;
            fire.remaining = 3f;
            fire.hitRadius = 1f;
            return fire;
        }

        internal void CancelMageFire() => Despawn();

        internal static void LaunchElfSword(CharacterInstance source, CharacterInstance target, Vector3 direction, int damage)
        {
            var sword = Create(source, target, source.CombatAimPoint, direction, damage, spell: false);
            sword.gameObject.name = "Elf sword projectile";
            sword.transform.localScale = new Vector3(.6f, .08f, .35f);
            sword.speed = 7f;
            sword.remaining = 2f;
            sword.hitRadius = 1f;
            sword.pierces = true;
        }

        internal static void LaunchMilipedeSpit(CharacterInstance source, CharacterInstance target, int damage)
        {
            Vector3 origin = source.CombatAimPoint;
            Vector2 scatter = Random.insideUnitCircle * 3f;
            Vector3 destination = target.CombatAimPoint + new Vector3(scatter.x, 0f, scatter.y);
            var flight = Create(source, target, origin, source.transform.forward, damage, spell: true);
            flight.name = "Milipede spit";
            flight.milipedeSpit = true;
            flight.arcOrigin = origin;
            flight.arcDestination = destination;
            flight.remaining = MilipedeFlightTime;
            flight.hitRadius = 1f;
            flight.ApplyMilipedeSpitSparks();
        }

        internal static SkeletonBowArrow LaunchSpiderSplash(CharacterInstance source, CharacterInstance target,
            Vector3 destination, int damage)
        {
            Vector3 origin = source.CombatAimPoint;
            var flight = Create(source, target, origin, source.transform.forward, damage, spell: true);
            flight.name = "Insane spider splash";
            flight.spiderSplash = true;
            flight.arcOrigin = origin;
            flight.arcDestination = destination;
            flight.remaining = SpiderSplashFlightTime;
            flight.transform.localScale = Vector3.one * .4f;
            return flight;
        }

        void ApplyMilipedeSpitSparks()
        {
            var material = Resources.Load<Material>("Jotunnslayer/Milipede/MilipedeSpit");
            if (!material || !material.mainTexture) return;

            GetComponent<MeshRenderer>().enabled = false;
            var effect = new GameObject("Milipede spit sparks");
            effect.transform.SetParent(transform, false);
            var sparks = effect.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, 1f);
            main.startColor = new Color(.386f, .802f, .382f);
            var emission = sparks.emission;
            emission.rateOverTime = 5f;
            emission.rateOverDistance = 8f;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, .234f), new GradientAlphaKey(0f, 1f) });
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            fade.color = gradient;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            sparks.Play();
        }

        internal static SkeletonBowArrow LaunchChampionMissile(CharacterInstance source, CharacterInstance target,
            int index, int damage)
        {
            Vector3 origin = source.CombatAimPoint + Vector3.up;
            Vector3 aim = (target.CombatAimPoint - origin).normalized;
            Vector3 direction = Quaternion.AngleAxis(-30f + 20f * index, Vector3.up) * aim;
            var missile = Create(source, target, origin, direction, damage, spell: true);
            missile.name = "Elemental champion missile";
            missile.speed = 6.7f;
            missile.remaining = 5f;
            missile.hitRadius = .3f;
            missile.championMissile = true;
            return missile;
        }

        bool championMissile;
        internal void CancelChampionMissile() => Despawn();

        internal static void LaunchHybBossMissile(CharacterInstance source, CharacterInstance target,
            Vector3 forward, float spread, int damage)
        {
            Vector3 position = source.transform.position + forward * 4.5f + Vector3.up * 5f;
            Vector3 aim = (target.CombatAimPoint - position).normalized;
            Vector3 direction = Quaternion.AngleAxis(spread, Vector3.up) * aim;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            direction = Quaternion.AngleAxis(-Random.Range(0f, 20f), right) * direction;
            var flight = Create(source, target, position, direction, damage, spell: true);
            flight.name = "Hyb boss missile";
            flight.speed = 6.7f;
            flight.remaining = 5f;
            flight.hitRadius = .3f;
            flight.reflectable = true;
            flight.hybBossMissile = true;
        }

        internal static void LaunchDriderOrb(CharacterInstance source, CharacterInstance target, Vector3 direction, int damage)
        {
            var orb = Create(source, target, source.CombatAimPoint, direction, damage, spell: true);
            orb.gameObject.name = "Drider orb";
            orb.speed = 4f;
            orb.remaining = 5f;
            orb.hitRadius = .45f;
            orb.pierces = orb.piercesTerrain = true;
            orb.gravitates = true;
        }

        internal static void LaunchElfChain(CharacterInstance source, CharacterInstance target, Vector3 direction, int damage) =>
            LaunchElfArrow(source, target, source.CombatAimPoint, direction, damage, splits: true);

        static void LaunchElfArrow(CharacterInstance source, CharacterInstance target, Vector3 position,
            Vector3 direction, int damage, bool splits)
        {
            var flight = Create(source, target, position, direction, damage, spell: false);
            flight.gameObject.name = splits ? "Elf chain shot" : "Elf chain branch";
            flight.SetArrowReadability(new Color(.4f, 1f, .2f));
            flight.speed = 7f;
            flight.remaining = 1.3f;
            flight.hitRadius = .5f;
            flight.pierces = flight.piercesTerrain = true;
            flight.splits = splits;
        }

        static SkeletonBowArrow Create(CharacterInstance source, CharacterInstance target, Vector3 position,
            Vector3 direction, int damage, bool spell)
        {
            var arrow = GameObject.CreatePrimitive(spell ? PrimitiveType.Sphere : PrimitiveType.Cube);
            arrow.name = spell ? "Hyb single missile" : "Skeleton arrow";
            arrow.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            arrow.transform.localScale = spell ? Vector3.one * .3f : new Vector3(.045f, .045f, .45f);
            var collider = arrow.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            var flight = arrow.AddComponent<SkeletonBowArrow>();
            flight.source = source;
            flight.target = target;
            flight.direction = direction;
            flight.damage = damage;
            return flight;
        }

        void SetArrowReadability(Color tint)
        {
            transform.localScale = new Vector3(.08f, .08f, .65f);
            arrowColor ??= new MaterialPropertyBlock();
            arrowColor.SetColor(BaseColor, tint);
            GetComponent<MeshRenderer>().SetPropertyBlock(arrowColor);
        }

        internal void CancelFlight() => Despawn(true);

        void Despawn(bool cancelled=false)
        {
            if(!enabled)return;
            enabled = false;
            if(draugArrow)draugArrow.FlightEnded(transform.position,hitTarget,cancelled);
            Destroy(gameObject);
        }

        void FixedUpdate()
        {
            if(draugArrow && (!source || !source.isServer || source.CurrentHealth<=0))
            {Despawn(true);return;}
            if(goblinReturningProjectile)
            {
                if(!source || !source.isServer) {Despawn();return;}
                TickGoblinReturn(Time.fixedDeltaTime);
                return;
            }
            if (!source || !source.isServer || remaining <= 0f)
            {
                Despawn();
                return;
            }

            float duration = Mathf.Min(remaining, Time.fixedDeltaTime);
            remaining -= duration;
            if (spiderSplash)
            {
                TickSpiderSplash();
                return;
            }
            float distance = speed * duration;
            Vector3 start = transform.position;
            if (milipedeSpit)
            {
                float t = 1f - remaining / MilipedeFlightTime;
                Vector3 step = Vector3.Lerp(arcOrigin, arcDestination, t) + Vector3.up * (20f * t * (1f - t)) - start;
                distance = step.magnitude;
                if (distance > .0001f) direction = step / distance;
            }
            else if ((hybBossMissile || championMissile) && !stoppedHoming && remaining <= 4.5f && target && target.CurrentHealth > 0)
            {
                Vector3 toTarget = target.CombatAimPoint - start;
                stoppedHoming = toTarget.sqrMagnitude <= 1.3f * 1.3f;
                if (!stoppedHoming)
                {
                    float turnRate = Mathf.Min(8f, 5f + (4.5f - remaining) * 3f);
                    direction = Vector3.RotateTowards(direction, toTarget.normalized, turnRate * duration, 0f);
                }
            }
            else if (gravitates && remaining <= (elementalPoisonOrb ? 5f : 4.5f) && target && target.CurrentHealth > 0)
            {
                Vector3 toTarget = (target.CombatAimPoint - start).normalized;
                direction = Vector3.RotateTowards(direction, toTarget, 2f * duration, 0f);
            }
            bool terrainHit = false;
            float terrainRadius = pierces ? .1f : hitRadius;
            if (!piercesTerrain && Physics.SphereCast(start, terrainRadius, direction, out var terrain, distance,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                terrainHit = true;
                distance = terrain.distance;
            }

            float trapContact = reflectable
                ? SkillBattlePreview.TimeTrapProjectileContact(source, start, direction, distance, Time.time)
                : float.PositiveInfinity;
            if (!hitTarget && target && target.CurrentHealth > 0 && target.Team != source.Team)
            {
                Vector3 toTarget = target.CombatAimPoint - start;
                float projection = Vector3.Dot(toTarget, direction);
                float radiusSquared = hitRadius * hitRadius;
                float lateralSquared = (toTarget - direction * projection).sqrMagnitude;
                if (lateralSquared <= radiusSquared)
                {
                    float chord = Mathf.Sqrt(radiusSquared - lateralSquared);
                    if (projection + chord >= 0f && projection - chord <= distance &&
                        Mathf.Max(0f, projection - chord) < trapContact)
                    {
                        if (reflectable && source.CurrentHealth > 0 &&
                            SkillBattlePreview.TryReflectRaidProjectile(target, source))
                        {
                            Reflect(start + direction * Mathf.Clamp(projection - chord, 0f, distance));
                            return;
                        }

                        int healthBefore = championMissile || elementalPoisonOrb ? target.CurrentHealth : 0;
                        target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, source, 0);
                        if (championMissile && target && source.TryGetComponent(out Skeleton1HCombat champion))
                            champion.RecordChampionMissileDamage(Mathf.Max(0, healthBefore - target.CurrentHealth));
                        if (elementalPoisonOrb && target && source.TryGetComponent(out Skeleton1HCombat poisonMage))
                            poisonMage.RecordPoisonOrbDamage(Mathf.Max(0, healthBefore - target.CurrentHealth));
                        hitTarget = true;
                        if(draugArrow)transform.position=start+direction*Mathf.Clamp(projection-chord,0f,distance);
                        if (!pierces)
                        {
                            Despawn();
                            return;
                        }
                    }
                }
            }

            if (terrainHit || !float.IsPositiveInfinity(trapContact))
            {
                if(draugArrow)transform.position=start+direction*Mathf.Min(distance,trapContact);
                Despawn(!float.IsPositiveInfinity(trapContact));
                return;
            }

            transform.position = start + direction * distance;
            if (!pierces || remaining > 0f) return;

            if (splits)
            {
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                LaunchElfArrow(source, target, transform.position + side * .5f, side, damage, splits: false);
                LaunchElfArrow(source, target, transform.position - side * .5f, -side, damage, splits: false);
            }
            Despawn();
        }

        void TickSpiderSplash()
        {
            if (source.CurrentHealth <= 0)
            {
                Despawn();
                return;
            }
            float progress = 1f - remaining / SpiderSplashFlightTime;
            Vector3 point = Vector3.Lerp(arcOrigin, arcDestination, progress) +
                Vector3.up * (3f * SpiderArc.Evaluate(progress));
            Vector3 step = point - transform.position;
            float length = step.magnitude;
            bool hitTerrain = false;
            if (length > .0001f && Physics.SphereCast(transform.position, .2f, step / length, out var terrain,
                length, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                point = transform.position + step / length * terrain.distance;
                hitTerrain = true;
            }
            transform.position = point;
            if (!hitTerrain && remaining > 0f) return;
            if (target && target.CurrentHealth > 0 && target.Team != source.Team &&
                (target.transform.position - point).sqrMagnitude <= SpiderSplashRadius * SpiderSplashRadius &&
                !Physics.Linecast(point, target.CombatAimPoint, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, source, 0);
            Despawn();
        }

        private void Reflect(Vector3 contact)
        {
            reflectable = false;
            (source, target) = (target, source);
            Vector3 offset = target.CombatAimPoint - contact;
            direction = offset.sqrMagnitude > 0.000001f ? offset.normalized : -direction;
            remaining = Mathf.Max(remaining, offset.magnitude / speed + .1f);
            transform.SetPositionAndRotation(contact, Quaternion.LookRotation(direction));
        }
    }
}
