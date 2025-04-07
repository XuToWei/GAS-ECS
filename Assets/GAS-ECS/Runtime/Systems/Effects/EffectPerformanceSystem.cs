using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Profiling;
using GAS.Core;
using GAS.Effects;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EffectPerformanceSystem : SystemBase
    {
        private EntityQuery effectQuery;
        private EntityQuery predictedEffectQuery;
        private EntityQuery serverStateQuery;
        private NativeHashMap<NetworkEntityId, NativeList<EffectState>> effectStates;
        private NativeHashMap<NetworkEntityId, int> effectCounts;
        private NativeHashMap<NetworkEntityId, float> effectProcessingTimes;
        private float maxProcessingTimePerFrame = 0.016f; // 16ms per frame
        private float effectBatchSize = 100f;
        private float effectPriorityThreshold = 0.8f;
        private float effectDistanceThreshold = 50f;
        private float effectTimeThreshold = 0.1f;

        private static readonly ProfilerMarker ProcessEffectsMarker = new ProfilerMarker("EffectPerformanceSystem.ProcessEffects");
        private static readonly ProfilerMarker OptimizeEffectsMarker = new ProfilerMarker("EffectPerformanceSystem.OptimizeEffects");
        private static readonly ProfilerMarker UpdateStatesMarker = new ProfilerMarker("EffectPerformanceSystem.UpdateStates");

        protected override void OnCreate()
        {
            effectQuery = GetEntityQuery(
                ComponentType.ReadOnly<EffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            predictedEffectQuery = GetEntityQuery(
                ComponentType.ReadOnly<PredictedEffectComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            serverStateQuery = GetEntityQuery(
                ComponentType.ReadOnly<ServerStateComponent>(),
                ComponentType.ReadOnly<NetworkEntity>()
            );

            effectStates = new NativeHashMap<NetworkEntityId, NativeList<EffectState>>(100, Allocator.Persistent);
            effectCounts = new NativeHashMap<NetworkEntityId, int>(100, Allocator.Persistent);
            effectProcessingTimes = new NativeHashMap<NetworkEntityId, float>(100, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            effectStates.Dispose();
            effectCounts.Dispose();
            effectProcessingTimes.Dispose();
        }

        protected override void OnUpdate()
        {
            using (ProcessEffectsMarker.Auto())
            {
                ProcessEffects();
            }

            using (OptimizeEffectsMarker.Auto())
            {
                OptimizeEffects();
            }

            using (UpdateStatesMarker.Auto())
            {
                UpdateStates();
            }
        }

        private void ProcessEffects()
        {
            var effects = effectQuery.ToEntityArray(Allocator.Temp);
            var predictedEffects = predictedEffectQuery.ToEntityArray(Allocator.Temp);
            var serverStates = serverStateQuery.ToEntityArray(Allocator.Temp);

            // 处理普通效果
            for (int i = 0; i < effects.Length; i++)
            {
                var entity = effects[i];
                var effect = SystemAPI.GetComponent<EffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!effectStates.ContainsKey(networkEntity.NetworkId))
                {
                    effectStates[networkEntity.NetworkId] = new NativeList<EffectState>(Allocator.Persistent);
                }

                var states = effectStates[networkEntity.NetworkId];
                states.Add(new EffectState
                {
                    Type = effect.Type,
                    Magnitude = effect.Magnitude,
                    Duration = effect.Duration,
                    Tags = effect.Tags,
                    Priority = effect.Priority,
                    ProcessingTime = 0f
                });

                // 更新效果计数
                if (!effectCounts.ContainsKey(networkEntity.NetworkId))
                {
                    effectCounts[networkEntity.NetworkId] = 0;
                }
                effectCounts[networkEntity.NetworkId]++;
            }

            // 处理预测效果
            for (int i = 0; i < predictedEffects.Length; i++)
            {
                var entity = predictedEffects[i];
                var effect = SystemAPI.GetComponent<PredictedEffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!effectStates.ContainsKey(networkEntity.NetworkId))
                {
                    effectStates[networkEntity.NetworkId] = new NativeList<EffectState>(Allocator.Persistent);
                }

                var states = effectStates[networkEntity.NetworkId];
                states.Add(new EffectState
                {
                    Type = effect.Type,
                    Magnitude = effect.Magnitude,
                    Duration = effect.Duration,
                    Tags = effect.Tags,
                    Priority = effect.Priority,
                    ProcessingTime = 0f,
                    IsPredicted = true
                });
            }

            // 处理服务器状态
            for (int i = 0; i < serverStates.Length; i++)
            {
                var entity = serverStates[i];
                var state = SystemAPI.GetComponent<ServerStateComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!effectStates.ContainsKey(networkEntity.NetworkId))
                {
                    effectStates[networkEntity.NetworkId] = new NativeList<EffectState>(Allocator.Persistent);
                }

                var states = effectStates[networkEntity.NetworkId];
                states.Add(new EffectState
                {
                    Type = state.Type,
                    Magnitude = state.Magnitude,
                    Duration = state.Duration,
                    Tags = state.Tags,
                    Priority = 0,
                    ProcessingTime = 0f,
                    IsServerState = true
                });
            }

            effects.Dispose();
            predictedEffects.Dispose();
            serverStates.Dispose();
        }

        private void OptimizeEffects()
        {
            foreach (var effectState in effectStates)
            {
                var networkId = effectState.Key;
                var states = effectState.Value;

                // 检查处理时间
                if (effectProcessingTimes.TryGetValue(networkId, out float processingTime) &&
                    processingTime > maxProcessingTimePerFrame)
                {
                    // 优化效果处理
                    OptimizeEffectProcessing(networkId, states);
                }

                // 检查效果数量
                if (effectCounts.TryGetValue(networkId, out int count) &&
                    count > effectBatchSize)
                {
                    // 优化效果数量
                    OptimizeEffectCount(networkId, states);
                }
            }
        }

        private void OptimizeEffectProcessing(NetworkEntityId networkId, NativeList<EffectState> states)
        {
            // 按优先级排序
            states.Sort(new EffectStateComparer());

            // 移除低优先级效果
            while (states.Length > 0 && states[0].Priority < effectPriorityThreshold)
            {
                states.RemoveAt(0);
            }

            // 合并相似效果
            for (int i = 1; i < states.Length; i++)
            {
                if (AreEffectsSimilar(states[i - 1], states[i]))
                {
                    states[i - 1].Magnitude += states[i].Magnitude;
                    states.RemoveAt(i);
                    i--;
                }
            }
        }

        private void OptimizeEffectCount(NetworkEntityId networkId, NativeList<EffectState> states)
        {
            // 按优先级排序
            states.Sort(new EffectStateComparer());

            // 保留高优先级效果
            while (states.Length > effectBatchSize)
            {
                states.RemoveAt(states.Length - 1);
            }
        }

        private bool AreEffectsSimilar(EffectState state1, EffectState state2)
        {
            if (state1.Type != state2.Type)
                return false;

            if (state1.Tags.Value.Tags.Length != state2.Tags.Value.Tags.Length)
                return false;

            for (int i = 0; i < state1.Tags.Value.Tags.Length; i++)
            {
                if (!state1.Tags.Value.Tags[i].Equals(state2.Tags.Value.Tags[i]))
                    return false;
            }

            return math.abs(state1.Magnitude - state2.Magnitude) < effectTimeThreshold;
        }

        private void UpdateStates()
        {
            foreach (var effectState in effectStates)
            {
                var networkId = effectState.Key;
                var states = effectState.Value;

                // 更新处理时间
                float totalProcessingTime = 0f;
                for (int i = 0; i < states.Length; i++)
                {
                    totalProcessingTime += states[i].ProcessingTime;
                }
                effectProcessingTimes[networkId] = totalProcessingTime;

                // 清理过期效果
                for (int i = states.Length - 1; i >= 0; i--)
                {
                    if (states[i].Duration <= 0f)
                    {
                        states.RemoveAt(i);
                    }
                }
            }
        }
    }

    public struct EffectStateComparer : IComparer<EffectState>
    {
        public int Compare(EffectState x, EffectState y)
        {
            return y.Priority.CompareTo(x.Priority);
        }
    }
} 