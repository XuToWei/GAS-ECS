using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Abilities;

namespace GAS.Core
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AbilitySystemManager : SystemBase
    {
        private EntityCommandBufferSystem beginSimECBSystem;
        private EntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;

        protected override void OnCreate()
        {
            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();
            var deltaTime = SystemAPI.Time.DeltaTime;

            // 更新所有能力系统
            Entities
                .WithAll<AbilitySystemComponent>()
                .ForEach((Entity entity, ref AbilitySystemComponent abilitySystem) =>
                {
                    UpdateAbilitySystem(entity, ref abilitySystem, deltaTime);
                })
                .Schedule();
        }

        private void UpdateAbilitySystem(Entity entity, ref AbilitySystemComponent abilitySystem, float deltaTime)
        {
            // 更新激活的能力
            for (int i = abilitySystem.ActiveAbilities.Length - 1; i >= 0; i--)
            {
                var ability = abilitySystem.ActiveAbilities[i];
                ability.Cooldown -= deltaTime;

                if (ability.Cooldown <= 0)
                {
                    abilitySystem.RemoveActiveAbility(i);
                }
                else
                {
                    abilitySystem.ActiveAbilities[i] = ability;
                }
            }

            // 更新激活的效果
            for (int i = abilitySystem.ActiveEffects.Length - 1; i >= 0; i--)
            {
                var effect = abilitySystem.ActiveEffects[i];
                effect.Duration -= deltaTime;

                if (effect.Duration <= 0)
                {
                    abilitySystem.RemoveActiveEffect(i);
                }
                else
                {
                    abilitySystem.ActiveEffects[i] = effect;
                }
            }
        }

        public bool TryActivateAbility(Entity owner, FixedString32 abilityName, ref AbilitySystemComponent abilitySystem)
        {
            if (!abilitySystem.AbilitySet.IsCreated)
                return false;

            var abilitySet = abilitySystem.AbilitySet.Value;
            for (int i = 0; i < abilitySet.Abilities.Length; i++)
            {
                var ability = abilitySet.Abilities[i];
                if (ability.Name.Equals(abilityName))
                {
                    // 检查条件
                    if (!CheckAbilityConditions(owner, ability, ref abilitySystem))
                        return false;

                    // 检查冷却
                    if (IsAbilityOnCooldown(abilityName, ref abilitySystem))
                        return false;

                    // 检查消耗
                    if (!CheckAbilityCost(ability, ref abilitySystem))
                        return false;

                    // 激活能力
                    ActivateAbility(owner, ability, ref abilitySystem);
                    return true;
                }
            }

            return false;
        }

        private bool CheckAbilityConditions(Entity owner, AbilityData ability, ref AbilitySystemComponent abilitySystem)
        {
            var conditionChecker = new CustomConditionChecker();
            for (int i = 0; i < ability.Conditions.Length; i++)
            {
                if (!conditionChecker.CheckCondition(owner, ability.Conditions[i], ref abilitySystem))
                    return false;
            }
            return true;
        }

        private bool IsAbilityOnCooldown(FixedString32 abilityName, ref AbilitySystemComponent abilitySystem)
        {
            for (int i = 0; i < abilitySystem.ActiveAbilities.Length; i++)
            {
                var activeAbility = abilitySystem.ActiveAbilities[i];
                if (activeAbility.Name.Equals(abilityName) && activeAbility.Cooldown > 0)
                    return true;
            }
            return false;
        }

        private bool CheckAbilityCost(AbilityData ability, ref AbilitySystemComponent abilitySystem)
        {
            var currentEnergy = abilitySystem.GetAttributeValue(new FixedString32("Energy"));
            return currentEnergy >= ability.Cost;
        }

        private void ActivateAbility(Entity owner, AbilityData ability, ref AbilitySystemComponent abilitySystem)
        {
            // 消耗能量
            var currentEnergy = abilitySystem.GetAttributeValue(new FixedString32("Energy"));
            abilitySystem.SetAttributeValue(new FixedString32("Energy"), currentEnergy - ability.Cost);

            // 添加激活的能力
            var activeAbility = new ActiveAbility
            {
                Name = ability.Name,
                Cooldown = ability.Cooldown,
                Cost = ability.Cost,
                PredictionKey = abilitySystem.GeneratePredictionKey(),
                IsActive = true
            };
            abilitySystem.AddActiveAbility(activeAbility);

            // 应用效果
            for (int i = 0; i < ability.Effects.Length; i++)
            {
                var effect = ability.Effects[i];
                ApplyEffect(owner, effect, ref abilitySystem);
            }

            // 处理触发器
            var triggerHandler = new CustomTriggerHandler();
            for (int i = 0; i < ability.Triggers.Length; i++)
            {
                triggerHandler.HandleTrigger(owner, ability.Triggers[i], ref abilitySystem, ref endSimECB);
            }
        }

        private void ApplyEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem)
        {
            // 创建效果实体
            var effectEntity = endSimECB.CreateEntity();
            endSimECB.AddComponent(effectEntity, new EffectComponent
            {
                Owner = owner,
                Type = effect.Type,
                Magnitude = effect.Magnitude,
                Duration = effect.Duration,
                Tags = effect.Tags,
                CustomData = effect.CustomData
            });

            // 添加激活的效果
            var activeEffect = new ActiveEffect
            {
                Name = new FixedString32(effect.Type.ToString()),
                Duration = effect.Duration,
                Magnitude = effect.Magnitude,
                PredictionKey = abilitySystem.GeneratePredictionKey(),
                IsActive = true
            };
            abilitySystem.AddActiveEffect(activeEffect);
        }
    }

    public struct EffectComponent : IComponentData
    {
        public Entity Owner;
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobArray<FixedString32> Tags;
        public BlobArray<byte> CustomData;
    }
} 