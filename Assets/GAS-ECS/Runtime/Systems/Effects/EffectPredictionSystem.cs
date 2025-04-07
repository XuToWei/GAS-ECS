using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Network;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public partial class EffectPredictionSystem : SystemBase
    {
        private EntityQuery pendingEffectQuery;
        private EntityQuery predictedEffectQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;
        private NativeHashMap<NetworkEntityId, NativeList<PredictedEffectState>> predictedEffects;
        private float predictionTimeWindow = 0.5f; // 预测时间窗口

        protected override void OnCreate()
        {
            pendingEffectQuery = GetEntityQuery(
                ComponentType.ReadOnly<PendingEffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            predictedEffectQuery = GetEntityQuery(
                ComponentType.ReadOnly<PredictedEffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            predictedEffects = new NativeHashMap<NetworkEntityId, NativeList<PredictedEffectState>>(100, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            predictedEffects.Dispose();
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            var deltaTime = SystemAPI.Time.DeltaTime;

            // 处理待处理效果
            ProcessPendingEffects(deltaTime);

            // 更新预测效果
            UpdatePredictedEffects(deltaTime);

            // 验证预测效果
            ValidatePredictedEffects();
        }

        private void ProcessPendingEffects(float deltaTime)
        {
            var pendingEffects = pendingEffectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < pendingEffects.Length; i++)
            {
                var entity = pendingEffects[i];
                var pendingEffect = SystemAPI.GetComponent<PendingEffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                // 检查是否到达应用时间
                pendingEffect.RemainingTime -= deltaTime;
                if (pendingEffect.RemainingTime <= 0)
                {
                    // 创建预测效果实体
                    var predictedEntity = beginSimECB.CreateEntity();
                    var predictedEffect = new PredictedEffectComponent
                    {
                        Owner = pendingEffect.Owner,
                        Type = pendingEffect.Type,
                        Magnitude = pendingEffect.Magnitude,
                        Duration = pendingEffect.Duration,
                        Period = pendingEffect.Period,
                        ChainRange = pendingEffect.ChainRange,
                        ChainDamageReduction = pendingEffect.ChainDamageReduction,
                        AreaRadius = pendingEffect.AreaRadius,
                        AreaDamageReduction = pendingEffect.AreaDamageReduction,
                        Priority = pendingEffect.Priority,
                        Tags = pendingEffect.Tags,
                        PredictionTime = predictionTimeWindow
                    };

                    beginSimECB.AddComponent(predictedEntity, predictedEffect);
                    beginSimECB.AddComponent(predictedEntity, networkEntity);

                    // 添加到预测效果列表
                    if (!predictedEffects.ContainsKey(networkEntity.NetworkId))
                    {
                        predictedEffects[networkEntity.NetworkId] = new NativeList<PredictedEffectState>(Allocator.Persistent);
                    }
                    predictedEffects[networkEntity.NetworkId].Add(new PredictedEffectState
                    {
                        Entity = predictedEntity,
                        Effect = predictedEffect
                    });

                    // 销毁待处理效果实体
                    endSimECB.DestroyEntity(entity);
                }
                else
                {
                    SystemAPI.SetComponent(entity, pendingEffect);
                }
            }
            pendingEffects.Dispose();
        }

        private void UpdatePredictedEffects(float deltaTime)
        {
            var predictedEffects = predictedEffectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < predictedEffects.Length; i++)
            {
                var entity = predictedEffects[i];
                var predictedEffect = SystemAPI.GetComponent<PredictedEffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                // 更新预测时间
                predictedEffect.PredictionTime -= deltaTime;
                if (predictedEffect.PredictionTime <= 0)
                {
                    // 移除预测效果
                    if (predictedEffects.ContainsKey(networkEntity.NetworkId))
                    {
                        var effects = predictedEffects[networkEntity.NetworkId];
                        for (int j = 0; j < effects.Length; j++)
                        {
                            if (effects[j].Entity == entity)
                            {
                                effects.RemoveAtSwapBack(j);
                                break;
                            }
                        }
                    }

                    // 销毁预测效果实体
                    endSimECB.DestroyEntity(entity);
                }
                else
                {
                    // 应用预测效果
                    ApplyPredictedEffect(entity, predictedEffect);
                    SystemAPI.SetComponent(entity, predictedEffect);
                }
            }
            predictedEffects.Dispose();
        }

        private void ValidatePredictedEffects()
        {
            // 检查服务器确认
            var serverConfirmations = GetEntityQuery(
                ComponentType.ReadOnly<ServerEffectConfirmationComponent>()
            ).ToEntityArray(Allocator.Temp);

            for (int i = 0; i < serverConfirmations.Length; i++)
            {
                var entity = serverConfirmations[i];
                var confirmation = SystemAPI.GetComponent<ServerEffectConfirmationComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (predictedEffects.ContainsKey(networkEntity.NetworkId))
                {
                    var effects = predictedEffects[networkEntity.NetworkId];
                    for (int j = 0; j < effects.Length; j++)
                    {
                        var predictedState = effects[j];
                        if (ValidatePrediction(predictedState.Effect, confirmation))
                        {
                            // 预测正确，转换为实际效果
                            ConvertToActualEffect(predictedState.Entity, predictedState.Effect);
                            effects.RemoveAtSwapBack(j);
                            break;
                        }
                        else
                        {
                            // 预测错误，回滚效果
                            RollbackPredictedEffect(predictedState.Entity, predictedState.Effect);
                            effects.RemoveAtSwapBack(j);
                            break;
                        }
                    }
                }

                // 销毁确认实体
                endSimECB.DestroyEntity(entity);
            }
            serverConfirmations.Dispose();
        }

        private bool ValidatePrediction(PredictedEffectComponent predicted, ServerEffectConfirmationComponent confirmation)
        {
            // 验证效果类型
            if (predicted.Type != confirmation.Type)
                return false;

            // 验证效果数值
            if (math.abs(predicted.Magnitude - confirmation.Magnitude) > 0.01f)
                return false;

            // 验证标签
            if (predicted.Tags.Value.Tags.Length != confirmation.Tags.Value.Tags.Length)
                return false;

            for (int i = 0; i < predicted.Tags.Value.Tags.Length; i++)
            {
                if (!predicted.Tags.Value.Tags[i].Equals(confirmation.Tags.Value.Tags[i]))
                    return false;
            }

            return true;
        }

        private void ConvertToActualEffect(Entity entity, PredictedEffectComponent predicted)
        {
            // 移除预测组件
            endSimECB.RemoveComponent<PredictedEffectComponent>(entity);

            // 添加实际效果组件
            var effect = new EffectComponent
            {
                Owner = predicted.Owner,
                Type = predicted.Type,
                Magnitude = predicted.Magnitude,
                Duration = predicted.Duration,
                Period = predicted.Period,
                ChainRange = predicted.ChainRange,
                ChainDamageReduction = predicted.ChainDamageReduction,
                AreaRadius = predicted.AreaRadius,
                AreaDamageReduction = predicted.AreaDamageReduction,
                Priority = predicted.Priority,
                Tags = predicted.Tags
            };

            endSimECB.AddComponent(entity, effect);
        }

        private void RollbackPredictedEffect(Entity entity, PredictedEffectComponent predicted)
        {
            // 回滚效果
            switch (predicted.Type)
            {
                case EffectType.Damage:
                    RollbackDamageEffect(predicted.Owner, predicted.Magnitude);
                    break;
                case EffectType.Heal:
                    RollbackHealEffect(predicted.Owner, predicted.Magnitude);
                    break;
                case EffectType.Speed:
                    RollbackSpeedEffect(predicted.Owner);
                    break;
                case EffectType.Shield:
                    RollbackShieldEffect(predicted.Owner);
                    break;
                case EffectType.Energy:
                    RollbackEnergyEffect(predicted.Owner, predicted.Magnitude);
                    break;
            }

            // 销毁预测效果实体
            endSimECB.DestroyEntity(entity);
        }

        private void ApplyPredictedEffect(Entity entity, PredictedEffectComponent predicted)
        {
            // 应用预测效果
            switch (predicted.Type)
            {
                case EffectType.Damage:
                    ApplyPredictedDamageEffect(predicted.Owner, predicted.Magnitude);
                    break;
                case EffectType.Heal:
                    ApplyPredictedHealEffect(predicted.Owner, predicted.Magnitude);
                    break;
                case EffectType.Speed:
                    ApplyPredictedSpeedEffect(predicted.Owner);
                    break;
                case EffectType.Shield:
                    ApplyPredictedShieldEffect(predicted.Owner);
                    break;
                case EffectType.Energy:
                    ApplyPredictedEnergyEffect(predicted.Owner, predicted.Magnitude);
                    break;
            }
        }

        private void ApplyPredictedDamageEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var health = SystemAPI.GetComponent<HealthComponent>(target);
            health.CurrentHealth -= magnitude;
            SystemAPI.SetComponent(target, health);
        }

        private void ApplyPredictedHealEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var health = SystemAPI.GetComponent<HealthComponent>(target);
            health.CurrentHealth = math.min(health.CurrentHealth + magnitude, health.MaxHealth);
            SystemAPI.SetComponent(target, health);
        }

        private void ApplyPredictedSpeedEffect(Entity target)
        {
            if (!EntityManager.Exists(target))
                return;

            var movement = SystemAPI.GetComponent<MovementComponent>(target);
            movement.Speed *= 1.5f; // 假设速度提升50%
            SystemAPI.SetComponent(target, movement);
        }

        private void ApplyPredictedShieldEffect(Entity target)
        {
            if (!EntityManager.Exists(target))
                return;

            var shield = SystemAPI.GetComponent<ShieldComponent>(target);
            shield.CurrentShield = math.min(shield.CurrentShield + 100f, shield.MaxShield); // 假设护盾增加100
            SystemAPI.SetComponent(target, shield);
        }

        private void ApplyPredictedEnergyEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var energy = SystemAPI.GetComponent<EnergyComponent>(target);
            energy.CurrentEnergy = math.min(energy.CurrentEnergy + magnitude, energy.MaxEnergy);
            SystemAPI.SetComponent(target, energy);
        }

        private void RollbackDamageEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var health = SystemAPI.GetComponent<HealthComponent>(target);
            health.CurrentHealth += magnitude;
            SystemAPI.SetComponent(target, health);
        }

        private void RollbackHealEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var health = SystemAPI.GetComponent<HealthComponent>(target);
            health.CurrentHealth -= magnitude;
            SystemAPI.SetComponent(target, health);
        }

        private void RollbackSpeedEffect(Entity target)
        {
            if (!EntityManager.Exists(target))
                return;

            var movement = SystemAPI.GetComponent<MovementComponent>(target);
            movement.Speed /= 1.5f; // 恢复原始速度
            SystemAPI.SetComponent(target, movement);
        }

        private void RollbackShieldEffect(Entity target)
        {
            if (!EntityManager.Exists(target))
                return;

            var shield = SystemAPI.GetComponent<ShieldComponent>(target);
            shield.CurrentShield = math.max(shield.CurrentShield - 100f, 0f); // 移除护盾
            SystemAPI.SetComponent(target, shield);
        }

        private void RollbackEnergyEffect(Entity target, float magnitude)
        {
            if (!EntityManager.Exists(target))
                return;

            var energy = SystemAPI.GetComponent<EnergyComponent>(target);
            energy.CurrentEnergy = math.max(energy.CurrentEnergy - magnitude, 0f);
            SystemAPI.SetComponent(target, energy);
        }
    }

    public struct PredictedEffectState
    {
        public Entity Entity;
        public PredictedEffectComponent Effect;
    }
} 