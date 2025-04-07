using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.NetCode;
using Unity.Transforms;
using GAS.Core;
using GAS.Effects;

namespace GAS.Network
{
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public partial class EffectNetworkSystem : SystemBase
    {
        private EntityQuery effectQuery;
        private EntityQuery pendingEffectQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;

        protected override void OnCreate()
        {
            effectQuery = GetEntityQuery(
                ComponentType.ReadOnly<EffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            pendingEffectQuery = GetEntityQuery(
                ComponentType.ReadOnly<PendingEffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            var deltaTime = SystemAPI.Time.DeltaTime;

            // 处理待处理的效果
            ProcessPendingEffects(deltaTime);

            // 同步效果状态
            SyncEffectStates();
        }

        private void ProcessPendingEffects(float deltaTime)
        {
            if (!SystemAPI.TryGetSingleton<NetworkStreamInGame>(out var networkStream))
                return;

            var pendingEffects = pendingEffectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < pendingEffects.Length; i++)
            {
                var entity = pendingEffects[i];
                var pendingEffect = SystemAPI.GetComponent<PendingEffectComponent>(entity);

                // 检查是否到达应用时间
                pendingEffect.TimeUntilApply -= deltaTime;
                if (pendingEffect.TimeUntilApply <= 0)
                {
                    // 创建效果实体
                    var effectEntity = CreateEffectEntity(pendingEffect);
                    if (effectEntity != Entity.Null)
                    {
                        // 添加网络组件
                        beginSimECB.AddComponent<NetworkEntity>(effectEntity);
                        beginSimECB.SetComponent(effectEntity, new NetworkEntity { NetworkId = networkStream.NextNetworkId });
                    }

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

        private void SyncEffectStates()
        {
            if (!SystemAPI.TryGetSingleton<NetworkStreamInGame>(out var networkStream))
                return;

            var effects = effectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < effects.Length; i++)
            {
                var entity = effects[i];
                var effect = SystemAPI.GetComponent<EffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                // 创建效果同步消息
                var effectSyncMessage = new EffectSyncMessage
                {
                    NetworkId = networkEntity.NetworkId,
                    OwnerNetworkId = effect.Owner.NetworkId,
                    Type = effect.Type,
                    Magnitude = effect.Magnitude,
                    Duration = effect.Duration,
                    Tags = effect.Tags
                };

                // 发送同步消息
                if (SystemAPI.HasSingleton<NetworkStreamDriver>())
                {
                    var driver = SystemAPI.GetSingleton<NetworkStreamDriver>();
                    driver.SendMessage(effectSyncMessage);
                }
            }
            effects.Dispose();
        }

        private Entity CreateEffectEntity(PendingEffectComponent pendingEffect)
        {
            var entity = beginSimECB.CreateEntity();

            // 添加效果组件
            var effectComponent = new EffectComponent
            {
                Owner = pendingEffect.Owner,
                Type = pendingEffect.Type,
                Magnitude = pendingEffect.Magnitude,
                Duration = pendingEffect.Duration,
                Tags = pendingEffect.Tags
            };
            beginSimECB.AddComponent(entity, effectComponent);

            // 根据效果类型添加特定组件
            switch (pendingEffect.Type)
            {
                case EffectType.Periodic:
                    beginSimECB.AddComponent(entity, new PeriodicEffectComponent
                    {
                        Timer = pendingEffect.Interval,
                        Interval = pendingEffect.Interval
                    });
                    break;
                case EffectType.Chain:
                    beginSimECB.AddComponent(entity, new ChainEffectComponent
                    {
                        RemainingChains = pendingEffect.ChainCount,
                        ChainRange = pendingEffect.ChainRange,
                        ChainDamageReduction = pendingEffect.ChainDamageReduction
                    });
                    break;
                case EffectType.Area:
                    beginSimECB.AddComponent(entity, new AreaEffectComponent
                    {
                        RemainingDuration = pendingEffect.Duration,
                        Radius = pendingEffect.Radius,
                        DamageReduction = pendingEffect.DamageReduction
                    });
                    break;
                case EffectType.Custom:
                    beginSimECB.AddComponent(entity, new CustomEffectComponent
                    {
                        Handler = pendingEffect.CustomHandler
                    });
                    break;
            }

            return entity;
        }
    }

    public struct PendingEffectComponent : IComponentData
    {
        public NetworkEntityId Owner;
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobAssetReference<EffectTagsBlob> Tags;
        public float TimeUntilApply;
        public float Interval;
        public int ChainCount;
        public float ChainRange;
        public float ChainDamageReduction;
        public float Radius;
        public float DamageReduction;
        public IEffectHandler CustomHandler;
    }

    public struct EffectSyncMessage : INetworkMessage
    {
        public NetworkEntityId NetworkId;
        public NetworkEntityId OwnerNetworkId;
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobAssetReference<EffectTagsBlob> Tags;
    }
} 