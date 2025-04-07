using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Network;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public partial class EffectRollbackSystem : SystemBase
    {
        private EntityQuery predictedEffectQuery;
        private EntityQuery serverStateQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;
        private NativeHashMap<NetworkEntityId, NativeList<EffectState>> serverStates;
        private NativeHashMap<NetworkEntityId, NativeList<EffectState>> clientStates;
        private float stateThreshold = 0.01f; // 状态差异阈值

        protected override void OnCreate()
        {
            predictedEffectQuery = GetEntityQuery(
                ComponentType.ReadOnly<PredictedEffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            serverStateQuery = GetEntityQuery(
                ComponentType.ReadOnly<ServerStateComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            serverStates = new NativeHashMap<NetworkEntityId, NativeList<EffectState>>(100, Allocator.Persistent);
            clientStates = new NativeHashMap<NetworkEntityId, NativeList<EffectState>>(100, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            serverStates.Dispose();
            clientStates.Dispose();
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            // 更新服务器状态
            UpdateServerStates();

            // 更新客户端状态
            UpdateClientStates();

            // 检测不一致
            DetectInconsistencies();

            // 执行回滚
            ExecuteRollbacks();
        }

        private void UpdateServerStates()
        {
            var serverStates = serverStateQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < serverStates.Length; i++)
            {
                var entity = serverStates[i];
                var serverState = SystemAPI.GetComponent<ServerStateComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!this.serverStates.ContainsKey(networkEntity.NetworkId))
                {
                    this.serverStates[networkEntity.NetworkId] = new NativeList<EffectState>(Allocator.Persistent);
                }

                var states = this.serverStates[networkEntity.NetworkId];
                states.Clear();
                states.Add(new EffectState
                {
                    Type = serverState.Type,
                    Magnitude = serverState.Magnitude,
                    Duration = serverState.Duration,
                    Tags = serverState.Tags
                });

                // 销毁服务器状态实体
                endSimECB.DestroyEntity(entity);
            }
            serverStates.Dispose();
        }

        private void UpdateClientStates()
        {
            var predictedEffects = predictedEffectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < predictedEffects.Length; i++)
            {
                var entity = predictedEffects[i];
                var predictedEffect = SystemAPI.GetComponent<PredictedEffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!clientStates.ContainsKey(networkEntity.NetworkId))
                {
                    clientStates[networkEntity.NetworkId] = new NativeList<EffectState>(Allocator.Persistent);
                }

                var states = clientStates[networkEntity.NetworkId];
                states.Clear();
                states.Add(new EffectState
                {
                    Type = predictedEffect.Type,
                    Magnitude = predictedEffect.Magnitude,
                    Duration = predictedEffect.Duration,
                    Tags = predictedEffect.Tags
                });
            }
            predictedEffects.Dispose();
        }

        private void DetectInconsistencies()
        {
            foreach (var serverState in serverStates)
            {
                var networkId = serverState.Key;
                var serverEffects = serverState.Value;

                if (clientStates.ContainsKey(networkId))
                {
                    var clientEffects = clientStates[networkId];
                    for (int i = 0; i < clientEffects.Length; i++)
                    {
                        var clientEffect = clientEffects[i];
                        bool isConsistent = false;

                        for (int j = 0; j < serverEffects.Length; j++)
                        {
                            var serverEffect = serverEffects[j];
                            if (AreEffectsConsistent(clientEffect, serverEffect))
                            {
                                isConsistent = true;
                                break;
                            }
                        }

                        if (!isConsistent)
                        {
                            // 标记需要回滚的效果
                            clientEffect.NeedsRollback = true;
                            clientEffects[i] = clientEffect;
                        }
                    }
                }
            }
        }

        private bool AreEffectsConsistent(EffectState client, EffectState server)
        {
            // 检查效果类型
            if (client.Type != server.Type)
                return false;

            // 检查效果数值
            if (math.abs(client.Magnitude - server.Magnitude) > stateThreshold)
                return false;

            // 检查持续时间
            if (math.abs(client.Duration - server.Duration) > stateThreshold)
                return false;

            // 检查标签
            if (client.Tags.Value.Tags.Length != server.Tags.Value.Tags.Length)
                return false;

            for (int i = 0; i < client.Tags.Value.Tags.Length; i++)
            {
                if (!client.Tags.Value.Tags[i].Equals(server.Tags.Value.Tags[i]))
                    return false;
            }

            return true;
        }

        private void ExecuteRollbacks()
        {
            foreach (var clientState in clientStates)
            {
                var networkId = clientState.Key;
                var clientEffects = clientState.Value;

                for (int i = 0; i < clientEffects.Length; i++)
                {
                    var effect = clientEffects[i];
                    if (effect.NeedsRollback)
                    {
                        // 执行回滚
                        RollbackEffect(networkId, effect);
                    }
                }
            }
        }

        private void RollbackEffect(NetworkEntityId networkId, EffectState effect)
        {
            var entity = GetEntityByNetworkId(networkId);
            if (entity == Entity.Null)
                return;

            switch (effect.Type)
            {
                case EffectType.Damage:
                    RollbackDamageEffect(entity, effect.Magnitude);
                    break;
                case EffectType.Heal:
                    RollbackHealEffect(entity, effect.Magnitude);
                    break;
                case EffectType.Speed:
                    RollbackSpeedEffect(entity);
                    break;
                case EffectType.Shield:
                    RollbackShieldEffect(entity);
                    break;
                case EffectType.Energy:
                    RollbackEnergyEffect(entity, effect.Magnitude);
                    break;
            }
        }

        private Entity GetEntityByNetworkId(NetworkEntityId networkId)
        {
            var networkQuery = GetEntityQuery(typeof(NetworkEntity));
            var entities = networkQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);
                if (networkEntity.NetworkId == networkId)
                {
                    entities.Dispose();
                    return entity;
                }
            }
            entities.Dispose();
            return Entity.Null;
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

    public struct EffectState
    {
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobAssetReference<EffectTagsBlob> Tags;
        public bool NeedsRollback;
    }
} 