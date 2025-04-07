using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;

namespace GAS.Effects
{
    public interface IEffectHandler
    {
        void ApplyEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB);
        void RemoveEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem);
    }

    public struct ChainEffectHandler : IEffectHandler
    {
        public void ApplyEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB)
        {
            // 解析链式效果数据
            var chainData = effect.CustomData;
            var chainCount = chainData.Length > 0 ? chainData[0] : 1;
            var chainRange = chainData.Length > 1 ? chainData[1] : 5f;
            var chainDamageMultiplier = chainData.Length > 2 ? chainData[2] / 100f : 0.8f;

            // 创建链式效果实体
            var chainEntity = endSimECB.CreateEntity();
            endSimECB.AddComponent(chainEntity, new ChainEffectComponent
            {
                Owner = owner,
                RemainingChains = chainCount,
                ChainRange = chainRange,
                DamageMultiplier = chainDamageMultiplier,
                CurrentDamage = effect.Magnitude,
                Tags = effect.Tags
            });
        }

        public void RemoveEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem)
        {
            // 清理链式效果
        }
    }

    public struct AreaEffectHandler : IEffectHandler
    {
        public void ApplyEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem, ref EntityCommandBuffer endSimECB)
        {
            // 解析区域效果数据
            var areaData = effect.CustomData;
            var radius = areaData.Length > 0 ? areaData[0] : 5f;
            var tickInterval = areaData.Length > 1 ? areaData[1] : 1f;
            var maxTargets = areaData.Length > 2 ? areaData[2] : 5;

            // 创建区域效果实体
            var areaEntity = endSimECB.CreateEntity();
            endSimECB.AddComponent(areaEntity, new AreaEffectComponent
            {
                Owner = owner,
                Radius = radius,
                TickInterval = tickInterval,
                MaxTargets = maxTargets,
                Damage = effect.Magnitude,
                RemainingDuration = effect.Duration,
                Tags = effect.Tags
            });
        }

        public void RemoveEffect(Entity owner, EffectData effect, ref AbilitySystemComponent abilitySystem)
        {
            // 清理区域效果
        }
    }

    public struct ChainEffectComponent : IComponentData
    {
        public Entity Owner;
        public byte RemainingChains;
        public float ChainRange;
        public float DamageMultiplier;
        public float CurrentDamage;
        public BlobArray<FixedString32> Tags;
    }

    public struct AreaEffectComponent : IComponentData
    {
        public Entity Owner;
        public float Radius;
        public float TickInterval;
        public byte MaxTargets;
        public float Damage;
        public float RemainingDuration;
        public BlobArray<FixedString32> Tags;
    }
} 