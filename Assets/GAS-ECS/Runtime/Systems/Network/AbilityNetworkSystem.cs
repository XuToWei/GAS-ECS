using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.NetCode;
using GAS.Core;

namespace GAS.Network
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AbilityNetworkSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem _beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem _endSimECBSystem;

        protected override void OnCreate()
        {
            _beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            _endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var beginSimECB = _beginSimECBSystem.CreateCommandBuffer();
            var endSimECB = _endSimECBSystem.CreateCommandBuffer();

            // 处理客户端预测
            if (!SystemAPI.HasSingleton<NetworkStreamInGame>())
                return;

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var isServer = SystemAPI.HasSingleton<NetworkStreamDriver>();
            var isClient = !isServer;

            // 处理能力激活请求
            Entities
                .WithAll<AbilitySystemComponent, NetworkStreamInGame>()
                .ForEach((Entity entity, ref AbilitySystemComponent abilitySystem) =>
                {
                    if (isClient)
                    {
                        // 客户端预测
                        ProcessClientPrediction(entity, ref abilitySystem, ref beginSimECB);
                    }
                    else
                    {
                        // 服务器验证
                        ProcessServerValidation(entity, ref abilitySystem, ref endSimECB);
                    }
                }).Schedule();
        }

        private void ProcessClientPrediction(Entity entity, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer beginSimECB)
        {
            // 获取预测键
            var predictionKey = abilitySystem.PredictionKey;

            // 创建预测实体
            var predictionEntity = beginSimECB.CreateEntity();
            beginSimECB.AddComponent(predictionEntity, new AbilityPredictionComponent
            {
                Owner = entity,
                PredictionKey = predictionKey,
                Timestamp = SystemAPI.Time.ElapsedTime
            });
        }

        private void ProcessServerValidation(Entity entity, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB)
        {
            // 验证客户端预测
            Entities
                .WithAll<AbilityPredictionComponent>()
                .ForEach((Entity predictionEntity, ref AbilityPredictionComponent prediction) =>
                {
                    if (prediction.Owner == entity)
                    {
                        // 验证预测键
                        if (prediction.PredictionKey == abilitySystem.PredictionKey)
                        {
                            // 预测成功，应用效果
                            ApplyPredictedEffects(entity, prediction, ref abilitySystem, ref endSimECB);
                        }
                        else
                        {
                            // 预测失败，回滚
                            RollbackPrediction(entity, prediction, ref abilitySystem);
                        }

                        endSimECB.DestroyEntity(predictionEntity);
                    }
                }).Run();
        }

        private void ApplyPredictedEffects(Entity entity, AbilityPredictionComponent prediction, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB)
        {
            // 应用预测的效果
            var abilitySet = abilitySystem.AbilitySet.Value;
            for (int i = 0; i < abilitySet.Abilities.Length; i++)
            {
                var ability = abilitySet.Abilities[i];
                for (int j = 0; j < ability.Effects.Length; j++)
                {
                    var effect = ability.Effects[j];
                    ApplyEffect(entity, effect, ref abilitySystem, ref endSimECB);
                }
            }
        }

        private void RollbackPrediction(Entity entity, AbilityPredictionComponent prediction, ref AbilitySystemComponent abilitySystem)
        {
            // 回滚预测的效果
            var abilitySet = abilitySystem.AbilitySet.Value;
            for (int i = 0; i < abilitySet.Abilities.Length; i++)
            {
                var ability = abilitySet.Abilities[i];
                for (int j = 0; j < ability.Effects.Length; j++)
                {
                    var effect = ability.Effects[j];
                    foreach (var tag in effect.Tags)
                    {
                        if (abilitySystem.Attributes.TryGetValue(tag, out float currentValue))
                        {
                            abilitySystem.Attributes[tag] = currentValue - effect.Magnitude;
                        }
                    }
                }
            }
        }

        private void ApplyEffect(Entity entity, EffectData effect, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB)
        {
            switch (effect.Type)
            {
                case EffectType.Instant:
                    foreach (var tag in effect.Tags)
                    {
                        if (abilitySystem.Attributes.TryGetValue(tag, out float currentValue))
                        {
                            abilitySystem.Attributes[tag] = currentValue + effect.Magnitude;
                        }
                    }
                    break;

                case EffectType.Duration:
                    var durationEntity = endSimECB.CreateEntity();
                    endSimECB.AddComponent(durationEntity, new DurationEffectComponent
                    {
                        Owner = entity,
                        Magnitude = effect.Magnitude,
                        RemainingDuration = effect.Duration,
                        Tags = effect.Tags
                    });
                    break;

                case EffectType.Periodic:
                    var periodicEntity = endSimECB.CreateEntity();
                    endSimECB.AddComponent(periodicEntity, new PeriodicEffectComponent
                    {
                        Owner = entity,
                        Magnitude = effect.Magnitude,
                        Duration = effect.Duration,
                        Period = 1.0f,
                        RemainingTime = effect.Duration,
                        NextTickTime = 0,
                        Tags = effect.Tags
                    });
                    break;
            }
        }
    }

    public struct AbilityPredictionComponent : IComponentData
    {
        public Entity Owner;
        public int PredictionKey;
        public double Timestamp;
    }
} 