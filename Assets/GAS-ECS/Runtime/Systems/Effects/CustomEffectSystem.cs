using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CustomEffectSystem : SystemBase
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

            // 处理链式效果
            Entities
                .WithAll<ChainEffectComponent>()
                .ForEach((Entity entity, ref ChainEffectComponent chainEffect) =>
                {
                    if (chainEffect.RemainingChains > 0)
                    {
                        // 查找下一个目标
                        var ownerPos = EntityManager.GetComponentData<LocalTransform>(chainEffect.Owner).Position;
                        var foundTarget = false;

                        Entities
                            .WithAll<AbilitySystemComponent>()
                            .ForEach((Entity targetEntity, ref AbilitySystemComponent targetSystem) =>
                            {
                                if (foundTarget || targetEntity == chainEffect.Owner)
                                    return;

                                var targetPos = EntityManager.GetComponentData<LocalTransform>(targetEntity).Position;
                                var distance = math.distance(ownerPos, targetPos);

                                if (distance <= chainEffect.ChainRange)
                                {
                                    // 应用链式伤害
                                    foreach (var tag in chainEffect.Tags)
                                    {
                                        if (targetSystem.Attributes.TryGetValue(tag, out float currentValue))
                                        {
                                            targetSystem.Attributes[tag] = currentValue - chainEffect.CurrentDamage;
                                        }
                                    }

                                    chainEffect.CurrentDamage *= chainEffect.DamageMultiplier;
                                    chainEffect.RemainingChains--;
                                    foundTarget = true;
                                }
                            }).Run();
                    }

                    if (chainEffect.RemainingChains == 0)
                    {
                        endSimECB.DestroyEntity(entity);
                    }
                }).Schedule();

            // 处理区域效果
            Entities
                .WithAll<AreaEffectComponent>()
                .ForEach((Entity entity, ref AreaEffectComponent areaEffect) =>
                {
                    areaEffect.RemainingDuration -= deltaTime;
                    if (areaEffect.RemainingDuration <= 0)
                    {
                        endSimECB.DestroyEntity(entity);
                        return;
                    }

                    // 检查是否需要应用伤害
                    var ownerPos = EntityManager.GetComponentData<LocalTransform>(areaEffect.Owner).Position;
                    var targetCount = 0;

                    Entities
                        .WithAll<AbilitySystemComponent>()
                        .ForEach((Entity targetEntity, ref AbilitySystemComponent targetSystem) =>
                        {
                            if (targetCount >= areaEffect.MaxTargets || targetEntity == areaEffect.Owner)
                                return;

                            var targetPos = EntityManager.GetComponentData<LocalTransform>(targetEntity).Position;
                            var distance = math.distance(ownerPos, targetPos);

                            if (distance <= areaEffect.Radius)
                            {
                                // 应用区域伤害
                                foreach (var tag in areaEffect.Tags)
                                {
                                    if (targetSystem.Attributes.TryGetValue(tag, out float currentValue))
                                    {
                                        targetSystem.Attributes[tag] = currentValue - areaEffect.Damage;
                                    }
                                }

                                targetCount++;
                            }
                        }).Run();
                }).Schedule();
        }
    }
} 