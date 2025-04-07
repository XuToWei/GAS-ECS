using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Network;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EffectProcessingSystem : SystemBase
    {
        private EntityQuery effectQuery;
        private EntityQuery abilitySystemQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;
        private EffectTargetFinder targetFinder;

        protected override void OnCreate()
        {
            effectQuery = GetEntityQuery(
                ComponentType.ReadOnly<EffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            abilitySystemQuery = GetEntityQuery(
                ComponentType.ReadOnly<AbilitySystemComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            targetFinder = new EffectTargetFinder(EntityManager);
        }

        protected override void OnDestroy()
        {
            targetFinder.Dispose();
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            var deltaTime = SystemAPI.Time.DeltaTime;
            var effects = effectQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < effects.Length; i++)
            {
                var entity = effects[i];
                var effect = SystemAPI.GetComponent<EffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!EntityManager.Exists(effect.Owner))
                {
                    endSimECB.DestroyEntity(entity);
                    continue;
                }

                var abilitySystem = SystemAPI.GetComponent<AbilitySystemComponent>(effect.Owner);

                switch (effect.Type)
                {
                    case EffectType.Instant:
                        ProcessInstantEffect(entity, ref effect, ref abilitySystem);
                        break;
                    case EffectType.Duration:
                        ProcessDurationEffect(entity, ref effect, ref abilitySystem, deltaTime);
                        break;
                    case EffectType.Periodic:
                        ProcessPeriodicEffect(entity, ref effect, ref abilitySystem, deltaTime);
                        break;
                    case EffectType.Chain:
                        ProcessChainEffect(entity, ref effect, ref abilitySystem);
                        break;
                    case EffectType.Area:
                        ProcessAreaEffect(entity, ref effect, ref abilitySystem);
                        break;
                    case EffectType.Custom:
                        ProcessCustomEffect(entity, ref effect, ref abilitySystem);
                        break;
                }
            }

            effects.Dispose();
        }

        private void ProcessInstantEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem)
        {
            ApplyEffect(effect.Owner, ref abilitySystem, effect.Magnitude, effect.Tags);
            endSimECB.DestroyEntity(entity);
        }

        private void ProcessDurationEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem, float deltaTime)
        {
            effect.Duration -= deltaTime;
            if (effect.Duration <= 0)
            {
                endSimECB.DestroyEntity(entity);
                return;
            }

            ApplyEffect(effect.Owner, ref abilitySystem, effect.Magnitude, effect.Tags);
            SystemAPI.SetComponent(entity, effect);
        }

        private void ProcessPeriodicEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem, float deltaTime)
        {
            effect.Duration -= deltaTime;
            if (effect.Duration <= 0)
            {
                endSimECB.DestroyEntity(entity);
                return;
            }

            effect.PeriodTimer -= deltaTime;
            if (effect.PeriodTimer <= 0)
            {
                ApplyEffect(effect.Owner, ref abilitySystem, effect.Magnitude, effect.Tags);
                effect.PeriodTimer = effect.Period;
            }

            SystemAPI.SetComponent(entity, effect);
        }

        private void ProcessChainEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem)
        {
            var chainData = effect.ChainData;
            var nextTarget = targetFinder.FindNextChainTarget(effect.Owner, chainData.ChainRange, chainData.ChainDamageReduction, EntityManager);

            if (nextTarget == Entity.Null)
            {
                endSimECB.DestroyEntity(entity);
                return;
            }

            var targetAbilitySystem = SystemAPI.GetComponent<AbilitySystemComponent>(nextTarget);
            ApplyEffect(nextTarget, ref targetAbilitySystem, effect.Magnitude * chainData.ChainDamageReduction, effect.Tags);
            endSimECB.DestroyEntity(entity);
        }

        private void ProcessAreaEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem)
        {
            var areaData = effect.AreaData;
            var targets = targetFinder.FindAreaTargets(effect.Owner, areaData.Radius, areaData.DamageReduction, EntityManager);

            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                var targetAbilitySystem = SystemAPI.GetComponent<AbilitySystemComponent>(target);
                ApplyEffect(target, ref targetAbilitySystem, effect.Magnitude * areaData.DamageReduction, effect.Tags);
            }

            targets.Dispose();
            endSimECB.DestroyEntity(entity);
        }

        private void ProcessCustomEffect(Entity entity, ref EffectComponent effect, ref AbilitySystemComponent abilitySystem)
        {
            // 处理自定义效果
            if (effect.CustomProcessor != Entity.Null)
            {
                var processor = SystemAPI.GetComponent<CustomEffectProcessor>(effect.CustomProcessor);
                processor.ProcessEffect(effect.Owner, ref abilitySystem, effect.Magnitude, effect.Tags);
            }
            endSimECB.DestroyEntity(entity);
        }

        private void ApplyEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude, BlobAssetReference<EffectTagsBlob> tags)
        {
            var tagArray = tags.Value.Tags;
            for (int i = 0; i < tagArray.Length; i++)
            {
                var tag = tagArray[i];
                switch (tag.ToString())
                {
                    case "Damage":
                        ApplyDamageEffect(target, ref abilitySystem, magnitude);
                        break;
                    case "Heal":
                        ApplyHealEffect(target, ref abilitySystem, magnitude);
                        break;
                    case "Speed":
                        ApplySpeedEffect(target, ref abilitySystem, magnitude);
                        break;
                    case "Shield":
                        ApplyShieldEffect(target, ref abilitySystem, magnitude);
                        break;
                    case "Energy":
                        ApplyEnergyEffect(target, ref abilitySystem, magnitude);
                        break;
                }
            }
        }

        private void ApplyDamageEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude)
        {
            var health = abilitySystem.GetAttributeValue(new FixedString32("Health"));
            var resistance = abilitySystem.GetAttributeValue(new FixedString32("Resistance"));
            var finalDamage = magnitude * (1 - resistance);
            abilitySystem.SetAttributeValue(new FixedString32("Health"), health - finalDamage);
        }

        private void ApplyHealEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude)
        {
            var health = abilitySystem.GetAttributeValue(new FixedString32("Health"));
            var maxHealth = abilitySystem.GetAttributeValue(new FixedString32("MaxHealth"));
            var benefit = abilitySystem.GetAttributeValue(new FixedString32("Benefit"));
            var finalHeal = magnitude * (1 + benefit);
            abilitySystem.SetAttributeValue(new FixedString32("Health"), math.min(health + finalHeal, maxHealth));
        }

        private void ApplySpeedEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude)
        {
            var speed = abilitySystem.GetAttributeValue(new FixedString32("Speed"));
            abilitySystem.SetAttributeValue(new FixedString32("Speed"), speed * magnitude);
        }

        private void ApplyShieldEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude)
        {
            var shield = abilitySystem.GetAttributeValue(new FixedString32("Shield"));
            abilitySystem.SetAttributeValue(new FixedString32("Shield"), shield + magnitude);
        }

        private void ApplyEnergyEffect(Entity target, ref AbilitySystemComponent abilitySystem, float magnitude)
        {
            var energy = abilitySystem.GetAttributeValue(new FixedString32("Energy"));
            var maxEnergy = abilitySystem.GetAttributeValue(new FixedString32("MaxEnergy"));
            abilitySystem.SetAttributeValue(new FixedString32("Energy"), math.min(energy + magnitude, maxEnergy));
        }
    }

    public struct PeriodicEffectComponent : IComponentData
    {
        public float Timer;
        public float Interval;
    }

    public struct ChainEffectComponent : IComponentData
    {
        public int RemainingChains;
        public float ChainRange;
        public float ChainDamageReduction;
    }

    public struct AreaEffectComponent : IComponentData
    {
        public float RemainingDuration;
        public float Radius;
        public float DamageReduction;
    }

    public struct CustomEffectComponent : IComponentData
    {
        public IEffectHandler Handler;
    }
} 