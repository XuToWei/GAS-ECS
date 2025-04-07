using UnityEngine;
using UnityEditor;
using Unity.Entities;
using System.Collections.Generic;
using GAS.Effects;

public class EffectDebugger : EditorWindow
{
    private bool showActiveEffects = true;
    private bool showPredictedEffects = true;
    private bool showServerStates = true;
    private bool showPerformance = true;
    private Vector2 scrollPosition;
    private Dictionary<Entity, float> effectProcessingTimes = new Dictionary<Entity, float>();
    private float updateInterval = 0.5f;
    private float lastUpdateTime;

    [MenuItem("GAS/Effect Debugger")]
    public static void ShowWindow()
    {
        GetWindow<EffectDebugger>("Effect Debugger");
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Effect Debugger", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 显示选项
        showActiveEffects = EditorGUILayout.Toggle("Show Active Effects", showActiveEffects);
        showPredictedEffects = EditorGUILayout.Toggle("Show Predicted Effects", showPredictedEffects);
        showServerStates = EditorGUILayout.Toggle("Show Server States", showServerStates);
        showPerformance = EditorGUILayout.Toggle("Show Performance", showPerformance);

        EditorGUILayout.Space();

        // 更新间隔设置
        updateInterval = EditorGUILayout.Slider("Update Interval", updateInterval, 0.1f, 2f);

        EditorGUILayout.Space();

        // 显示调试信息
        if (showActiveEffects)
        {
            DrawActiveEffects();
        }

        if (showPredictedEffects)
        {
            DrawPredictedEffects();
        }

        if (showServerStates)
        {
            DrawServerStates();
        }

        if (showPerformance)
        {
            DrawPerformance();
        }

        EditorGUILayout.EndScrollView();
    }

    private void OnEditorUpdate()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDebugInfo();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDebugInfo()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        effectProcessingTimes.Clear();

        // 更新效果处理时间
        var effectQuery = entityManager.CreateEntityQuery(typeof(EffectComponent));
        var effects = effectQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in effects)
        {
            var effect = entityManager.GetComponentData<EffectComponent>(entity);
            if (!effectProcessingTimes.ContainsKey(entity))
            {
                effectProcessingTimes[entity] = Time.time;
            }
        }
        effects.Dispose();
    }

    private void DrawActiveEffects()
    {
        EditorGUILayout.LabelField("Active Effects", EditorStyles.boldLabel);
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        var effectQuery = entityManager.CreateEntityQuery(typeof(EffectComponent));
        var effects = effectQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in effects)
        {
            var effect = entityManager.GetComponentData<EffectComponent>(entity);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"Effect: {effect.EffectData.Name}");
            EditorGUILayout.LabelField($"Type: {effect.EffectData.Type}");
            EditorGUILayout.LabelField($"Magnitude: {effect.EffectData.Magnitude}");
            EditorGUILayout.LabelField($"Duration: {effect.EffectData.Duration}");
            EditorGUILayout.LabelField($"Priority: {effect.EffectData.Priority}");
            
            if (effectProcessingTimes.ContainsKey(entity))
            {
                float processingTime = Time.time - effectProcessingTimes[entity];
                EditorGUILayout.LabelField($"Processing Time: {processingTime:F2}s");
            }

            EditorGUILayout.EndVertical();
        }
        effects.Dispose();
    }

    private void DrawPredictedEffects()
    {
        EditorGUILayout.LabelField("Predicted Effects", EditorStyles.boldLabel);
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        var predictedQuery = entityManager.CreateEntityQuery(typeof(PredictedEffectComponent));
        var predictedEffects = predictedQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in predictedEffects)
        {
            var predicted = entityManager.GetComponentData<PredictedEffectComponent>(entity);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"Target: {predicted.Target}");
            EditorGUILayout.LabelField($"Effect: {predicted.EffectData.Name}");
            EditorGUILayout.LabelField($"Predicted Value: {predicted.PredictedValue}");
            EditorGUILayout.LabelField($"Prediction Time: {predicted.PredictionTime:F2}s");
            
            EditorGUILayout.EndVertical();
        }
        predictedEffects.Dispose();
    }

    private void DrawServerStates()
    {
        EditorGUILayout.LabelField("Server States", EditorStyles.boldLabel);
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        var serverQuery = entityManager.CreateEntityQuery(typeof(ServerStateComponent));
        var serverStates = serverQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in serverStates)
        {
            var state = entityManager.GetComponentData<ServerStateComponent>(entity);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"Entity: {state.EntityId}");
            EditorGUILayout.LabelField($"Effect Type: {state.EffectType}");
            EditorGUILayout.LabelField($"Value: {state.Value}");
            EditorGUILayout.LabelField($"Timestamp: {state.Timestamp:F2}s");
            
            EditorGUILayout.EndVertical();
        }
        serverStates.Dispose();
    }

    private void DrawPerformance()
    {
        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        
        // 统计效果数量
        var effectQuery = entityManager.CreateEntityQuery(typeof(EffectComponent));
        var predictedQuery = entityManager.CreateEntityQuery(typeof(PredictedEffectComponent));
        var serverQuery = entityManager.CreateEntityQuery(typeof(ServerStateComponent));

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Active Effects: {effectQuery.CalculateEntityCount()}");
        EditorGUILayout.LabelField($"Predicted Effects: {predictedQuery.CalculateEntityCount()}");
        EditorGUILayout.LabelField($"Server States: {serverQuery.CalculateEntityCount()}");
        EditorGUILayout.EndVertical();

        // 显示处理时间统计
        if (effectProcessingTimes.Count > 0)
        {
            float avgProcessingTime = 0f;
            foreach (var time in effectProcessingTimes.Values)
            {
                avgProcessingTime += Time.time - time;
            }
            avgProcessingTime /= effectProcessingTimes.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Average Processing Time: {avgProcessingTime:F3}s");
            EditorGUILayout.EndVertical();
        }
    }
} 