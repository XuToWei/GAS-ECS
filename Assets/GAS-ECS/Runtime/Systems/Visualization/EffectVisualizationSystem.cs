using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using GAS.Core;
using GAS.Network;

namespace GAS.Effects
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EffectVisualizationSystem : SystemBase
    {
        private EntityQuery effectQuery;
        private EntityQuery predictedEffectQuery;
        private BeginSimulationEntityCommandBufferSystem beginSimECBSystem;
        private EndSimulationEntityCommandBufferSystem endSimECBSystem;
        private EntityCommandBuffer beginSimECB;
        private EntityCommandBuffer endSimECB;
        private EntityArchetype floatingTextArchetype;
        private EntityArchetype effectIconArchetype;
        private EntityArchetype effectParticleArchetype;
        private float4 damageColor = new float4(1, 0, 0, 1);
        private float4 healColor = new float4(0, 1, 0, 1);
        private float4 buffColor = new float4(1, 1, 0, 1);
        private float4 debuffColor = new float4(0.5f, 0, 0.5f, 1);

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

            beginSimECBSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            endSimECBSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            // 创建浮动文本原型
            floatingTextArchetype = EntityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(FloatingTextComponent),
                typeof(RenderMesh),
                typeof(RenderBounds)
            );

            // 创建效果图标原型
            effectIconArchetype = EntityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(EffectIconComponent),
                typeof(RenderMesh),
                typeof(RenderBounds)
            );

            // 创建效果粒子原型
            effectParticleArchetype = EntityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(EffectParticleComponent),
                typeof(RenderMesh),
                typeof(RenderBounds)
            );
        }

        protected override void OnUpdate()
        {
            beginSimECB = beginSimECBSystem.CreateCommandBuffer();
            endSimECB = endSimECBSystem.CreateCommandBuffer();

            // 处理效果可视化
            ProcessEffectVisualizations();
        }

        private void ProcessEffectVisualizations()
        {
            // 处理普通效果
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

                CreateEffectVisualization(effect.Owner, effect, false);
            }
            effects.Dispose();

            // 处理预测效果
            var predictedEffects = predictedEffectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < predictedEffects.Length; i++)
            {
                var entity = predictedEffects[i];
                var effect = SystemAPI.GetComponent<PredictedEffectComponent>(entity);
                var networkEntity = SystemAPI.GetComponent<NetworkEntity>(entity);

                if (!EntityManager.Exists(effect.Owner))
                {
                    endSimECB.DestroyEntity(entity);
                    continue;
                }

                CreateEffectVisualization(effect.Owner, effect, true);
            }
            predictedEffects.Dispose();
        }

        private void CreateEffectVisualization(Entity target, EffectComponent effect, bool isPredicted)
        {
            var targetTransform = SystemAPI.GetComponent<LocalTransform>(target);
            var tagArray = effect.Tags.Value.Tags;

            for (int i = 0; i < tagArray.Length; i++)
            {
                var tag = tagArray[i];
                switch (tag.ToString())
                {
                    case "Damage":
                        CreateDamageVisualization(targetTransform.Position, effect.Magnitude, isPredicted);
                        break;
                    case "Heal":
                        CreateHealVisualization(targetTransform.Position, effect.Magnitude, isPredicted);
                        break;
                    case "Speed":
                        CreateSpeedVisualization(targetTransform.Position, isPredicted);
                        break;
                    case "Shield":
                        CreateShieldVisualization(targetTransform.Position, isPredicted);
                        break;
                    case "Energy":
                        CreateEnergyVisualization(targetTransform.Position, isPredicted);
                        break;
                }
            }
        }

        private void CreateDamageVisualization(float3 position, float magnitude, bool isPredicted)
        {
            var color = isPredicted ? new float4(damageColor.rgb, 0.5f) : damageColor;
            CreateFloatingText(position, $"-{magnitude:F0}", color);
            CreateEffectParticle(position, "DamageParticle", color);
        }

        private void CreateHealVisualization(float3 position, float magnitude, bool isPredicted)
        {
            var color = isPredicted ? new float4(healColor.rgb, 0.5f) : healColor;
            CreateFloatingText(position, $"+{magnitude:F0}", color);
            CreateEffectParticle(position, "HealParticle", color);
        }

        private void CreateSpeedVisualization(float3 position, bool isPredicted)
        {
            var color = isPredicted ? new float4(buffColor.rgb, 0.5f) : buffColor;
            CreateEffectIcon(position, "SpeedIcon", color);
            CreateEffectParticle(position, "SpeedParticle", color);
        }

        private void CreateShieldVisualization(float3 position, bool isPredicted)
        {
            var color = isPredicted ? new float4(buffColor.rgb, 0.5f) : buffColor;
            CreateEffectIcon(position, "ShieldIcon", color);
            CreateEffectParticle(position, "ShieldParticle", color);
        }

        private void CreateEnergyVisualization(float3 position, bool isPredicted)
        {
            var color = isPredicted ? new float4(buffColor.rgb, 0.5f) : buffColor;
            CreateEffectIcon(position, "EnergyIcon", color);
            CreateEffectParticle(position, "EnergyParticle", color);
        }

        private void CreateFloatingText(float3 position, string text, float4 color)
        {
            var entity = beginSimECB.CreateEntity(floatingTextArchetype);
            var transform = LocalTransform.FromPosition(position);
            var floatingText = new FloatingTextComponent
            {
                Text = text,
                Color = color,
                Duration = 1.0f,
                RiseSpeed = 2.0f,
                FadeSpeed = 1.0f
            };

            beginSimECB.SetComponent(entity, transform);
            beginSimECB.SetComponent(entity, floatingText);
        }

        private void CreateEffectIcon(float3 position, string iconName, float4 color)
        {
            var entity = beginSimECB.CreateEntity(effectIconArchetype);
            var transform = LocalTransform.FromPosition(position);
            var effectIcon = new EffectIconComponent
            {
                IconName = iconName,
                Color = color,
                Duration = 1.0f,
                Scale = 1.0f
            };

            beginSimECB.SetComponent(entity, transform);
            beginSimECB.SetComponent(entity, effectIcon);
        }

        private void CreateEffectParticle(float3 position, string particleName, float4 color)
        {
            var entity = beginSimECB.CreateEntity(effectParticleArchetype);
            var transform = LocalTransform.FromPosition(position);
            var effectParticle = new EffectParticleComponent
            {
                ParticleName = particleName,
                Color = color,
                Duration = 1.0f,
                Scale = 1.0f
            };

            beginSimECB.SetComponent(entity, transform);
            beginSimECB.SetComponent(entity, effectParticle);
        }
    }

    public struct FloatingTextComponent : IComponentData
    {
        public FixedString64Bytes Text;
        public float4 Color;
        public float Duration;
        public float RiseSpeed;
        public float FadeSpeed;
    }

    public struct EffectIconComponent : IComponentData
    {
        public FixedString64Bytes IconName;
        public float4 Color;
        public float Duration;
        public float Scale;
    }

    public struct EffectParticleComponent : IComponentData
    {
        public FixedString64Bytes ParticleName;
        public float4 Color;
        public float Duration;
        public float Scale;
    }
} 