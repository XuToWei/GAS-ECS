using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EffectSystem : SystemBase
    {
        private EntityCommandBufferSystem _beginSimECBSystem;
        private EntityCommandBufferSystem _endSimECBSystem;

        protected override void OnCreate()
        {
            _beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            _endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var beginSimECB = _beginSimECBSystem.CreateCommandBuffer();
            var endSimECB = _endSimECBSystem.CreateCommandBuffer();
            var deltaTime = SystemAPI.Time.DeltaTime;

            // 处理持续时间效果
            Entities
                .WithAll<DurationEffectComponent>()
                .ForEach((Entity entity, ref DurationEffectComponent effect) =>
                {
                    effect.RemainingDuration -= deltaTime;
                    if (effect.RemainingDuration <= 0)
                    {
                        endSimECB.DestroyEntity(entity);
                    }
                }).Schedule();

            // 处理周期性效果
            Entities
                .WithAll<PeriodicEffectComponent>()
                .ForEach((Entity entity, ref PeriodicEffectComponent effect) =>
                {
                    effect.RemainingTime -= deltaTime;
                    if (effect.RemainingTime <= 0)
                    {
                        endSimECB.DestroyEntity(entity);
                        return;
                    }

                    effect.NextTickTime -= deltaTime;
                    if (effect.NextTickTime <= 0)
                    {
                        // 应用周期性效果
                        if (EntityManager.Exists(effect.Owner))
                        {
                            var abilitySystem = EntityManager.GetComponentData<AbilitySystemComponent>(effect.Owner);
                            foreach (var tag in effect.Tags)
                            {
                                if (abilitySystem.Attributes.TryGetValue(tag, out float currentValue))
                                {
                                    abilitySystem.Attributes[tag] = currentValue + effect.Magnitude;
                                }
                            }
                        }

                        effect.NextTickTime = effect.Period;
                    }
                }).Schedule();
        }
    }
} 