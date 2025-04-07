using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using GAS.Core;
using GAS.Network;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EffectCombinationSystem : SystemBase
    {
        private EntityQuery effectQuery;
        private EntityQuery abilitySystemQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;
        private NativeHashMap<NetworkEntityId, NativeList<EffectState>> effectGroups;
        private float combinationThreshold = 0.1f;

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

            effectGroups = new NativeHashMap<NetworkEntityId, NativeList<EffectState>>(100, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (effectGroups.IsCreated)
            {
                foreach (var group in effectGroups)
                {
                    group.Value.Dispose();
                }
                effectGroups.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            // 清理旧的效果组
            CleanupOldEffectGroups();

            // 收集效果
            CollectEffects();

            // 处理效果组合
            ProcessEffectCombinations();
        }

        private void CleanupOldEffectGroups()
        {
            var keys = effectGroups.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (effectGroups.TryGetValue(key, out var group))
                {
                    group.Clear();
                }
            }
            keys.Dispose();
        }

        private void CollectEffects()
        {
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

                var state = new EffectState
                {
                    OwnerNetworkId = effect.Owner,
                    Type = effect.Type,
                    Magnitude = effect.Magnitude,
                    Duration = effect.Duration,
                    Tags = effect.Tags,
                    Priority = effect.Priority,
                    IsPredicted = false
                };

                if (!effectGroups.TryGetValue(networkEntity.NetworkId, out var group))
                {
                    group = new NativeList<EffectState>(10, Allocator.Persistent);
                    effectGroups[networkEntity.NetworkId] = group;
                }
                group.Add(state);
            }
            effects.Dispose();
        }

        private void ProcessEffectCombinations()
        {
            var keys = effectGroups.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (effectGroups.TryGetValue(key, out var group))
                {
                    ProcessEffectGroup(key, group);
                }
            }
            keys.Dispose();
        }

        private void ProcessEffectGroup(NetworkEntityId networkId, NativeList<EffectState> group)
        {
            // 按优先级排序
            SortEffectsByPriority(group);

            // 处理效果组合
            for (int i = 0; i < group.Length; i++)
            {
                var currentEffect = group[i];
                var matchingEffects = new NativeList<EffectState>(Allocator.Temp);

                // 查找匹配的效果
                for (int j = i + 1; j < group.Length; j++)
                {
                    var otherEffect = group[j];
                    if (CanCombineEffects(currentEffect, otherEffect))
                    {
                        matchingEffects.Add(otherEffect);
                    }
                }

                if (matchingEffects.Length > 0)
                {
                    // 计算组合效果
                    var combinedEffect = CalculateCombinedEffect(currentEffect, matchingEffects);
                    ApplyCombinedEffect(networkId, combinedEffect);
                    i += matchingEffects.Length;
                }
                else
                {
                    // 应用单个效果
                    ApplySingleEffect(networkId, currentEffect);
                }

                matchingEffects.Dispose();
            }
        }

        private void SortEffectsByPriority(NativeList<EffectState> group)
        {
            // 使用冒泡排序按优先级排序
            for (int i = 0; i < group.Length - 1; i++)
            {
                for (int j = 0; j < group.Length - i - 1; j++)
                {
                    if (group[j].Priority < group[j + 1].Priority)
                    {
                        var temp = group[j];
                        group[j] = group[j + 1];
                        group[j + 1] = temp;
                    }
                }
            }
        }

        private bool CanCombineEffects(EffectState effect1, EffectState effect2)
        {
            // 检查效果类型是否匹配
            if (effect1.Type != effect2.Type)
                return false;

            // 检查标签是否匹配
            if (!AreTagsEqual(effect1.Tags, effect2.Tags))
                return false;

            // 检查优先级是否匹配
            if (math.abs(effect1.Priority - effect2.Priority) > combinationThreshold)
                return false;

            return true;
        }

        private EffectState CalculateCombinedEffect(EffectState baseEffect, NativeList<EffectState> matchingEffects)
        {
            var combinedEffect = baseEffect;
            var totalMagnitude = baseEffect.Magnitude;
            var maxDuration = baseEffect.Duration;

            // 计算组合后的效果值
            for (int i = 0; i < matchingEffects.Length; i++)
            {
                var effect = matchingEffects[i];
                totalMagnitude += effect.Magnitude;
                maxDuration = math.max(maxDuration, effect.Duration);
            }

            combinedEffect.Magnitude = totalMagnitude;
            combinedEffect.Duration = maxDuration;

            return combinedEffect;
        }

        private void ApplyCombinedEffect(NetworkEntityId networkId, EffectState effect)
        {
            var entity = GetEntityByNetworkId(networkId);
            if (entity == Entity.Null)
                return;

            var abilitySystem = SystemAPI.GetComponent<AbilitySystemComponent>(entity);
            ApplyEffect(entity, ref abilitySystem, effect.Magnitude, effect.Tags);
        }

        private void ApplySingleEffect(NetworkEntityId networkId, EffectState effect)
        {
            var entity = GetEntityByNetworkId(networkId);
            if (entity == Entity.Null)
                return;

            var abilitySystem = SystemAPI.GetComponent<AbilitySystemComponent>(entity);
            ApplyEffect(entity, ref abilitySystem, effect.Magnitude, effect.Tags);
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

        private bool AreTagsEqual(BlobAssetReference<EffectTagsBlob> tags1, BlobAssetReference<EffectTagsBlob> tags2)
        {
            if (tags1.Length != tags2.Length)
                return false;

            var tagArray1 = tags1.Value.Tags;
            var tagArray2 = tags2.Value.Tags;

            for (int i = 0; i < tagArray1.Length; i++)
            {
                if (tagArray1[i] != tagArray2[i])
                    return false;
            }

            return true;
        }
    }

    public struct EffectState
    {
        public NetworkEntityId OwnerNetworkId;
        public EffectType Type;
        public float Magnitude;
        public float Duration;
        public BlobAssetReference<EffectTagsBlob> Tags;
        public int Priority;
        public bool IsPredicted;
        public float Interval;
        public int ChainCount;
        public float ChainRange;
        public float ChainDamageReduction;
        public float Radius;
        public float DamageReduction;
        public IEffectHandler CustomHandler;
    }

    public struct EffectStateComparer : IComparer<EffectState>
    {
        public int Compare(EffectState x, EffectState y)
        {
            // 首先按优先级排序
            int priorityCompare = y.Priority.CompareTo(x.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            // 然后按类型排序
            return x.Type.CompareTo(y.Type);
        }
    }
} 