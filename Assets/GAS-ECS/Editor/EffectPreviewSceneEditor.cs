using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GAS.Effects;

public class EffectPreviewSceneEditor : EditorWindow
{
    private EffectData selectedEffect;
    private GameObject previewTarget;
    private GameObject previewSource;
    private Vector3 targetPosition = Vector3.zero;
    private Vector3 sourcePosition = new Vector3(2f, 0f, 0f);
    private bool showGizmos = true;
    private bool autoPlay = false;
    private float timeScale = 1f;
    private Vector2 scrollPosition;

    [MenuItem("GAS/Effect Preview Scene")]
    public static void ShowWindow()
    {
        GetWindow<EffectPreviewSceneEditor>("Effect Preview");
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

        EditorGUILayout.LabelField("Effect Preview Scene", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 效果选择
        EditorGUI.BeginChangeCheck();
        selectedEffect = (EffectData)EditorGUILayout.ObjectField("Effect", selectedEffect, typeof(EffectData), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (selectedEffect != null)
            {
                CreatePreviewScene();
            }
        }

        EditorGUILayout.Space();

        // 预览设置
        EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);
        showGizmos = EditorGUILayout.Toggle("Show Gizmos", showGizmos);
        autoPlay = EditorGUILayout.Toggle("Auto Play", autoPlay);
        timeScale = EditorGUILayout.Slider("Time Scale", timeScale, 0f, 2f);

        EditorGUILayout.Space();

        // 位置设置
        EditorGUILayout.LabelField("Position Settings", EditorStyles.boldLabel);
        targetPosition = EditorGUILayout.Vector3Field("Target Position", targetPosition);
        sourcePosition = EditorGUILayout.Vector3Field("Source Position", sourcePosition);

        EditorGUILayout.Space();

        // 控制按钮
        if (GUILayout.Button("Create Preview Scene"))
        {
            CreatePreviewScene();
        }

        if (GUILayout.Button("Reset Scene"))
        {
            ResetScene();
        }

        if (GUILayout.Button("Apply Effect"))
        {
            ApplyEffect();
        }

        EditorGUILayout.EndScrollView();
    }

    private void OnEditorUpdate()
    {
        if (autoPlay && selectedEffect != null)
        {
            ApplyEffect();
        }
    }

    private void CreatePreviewScene()
    {
        if (selectedEffect == null) return;

        // 创建新场景
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        // 创建目标
        previewTarget = CreatePreviewObject("Target", targetPosition);
        AddPreviewComponents(previewTarget);

        // 创建源
        previewSource = CreatePreviewObject("Source", sourcePosition);
        AddPreviewComponents(previewSource);

        // 设置场景视图
        SceneView.lastActiveSceneView.AlignViewToObject(previewTarget.transform);
    }

    private GameObject CreatePreviewObject(string name, Vector3 position)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * 0.5f;
        return obj;
    }

    private void AddPreviewComponents(GameObject obj)
    {
        // 添加必要的组件
        obj.AddComponent<HealthComponent>();
        obj.AddComponent<MovementComponent>();
        obj.AddComponent<ShieldComponent>();
        obj.AddComponent<EnergyComponent>();
        obj.AddComponent<NetworkEntity>();
    }

    private void ResetScene()
    {
        if (previewTarget != null)
        {
            previewTarget.transform.position = targetPosition;
            ResetComponents(previewTarget);
        }

        if (previewSource != null)
        {
            previewSource.transform.position = sourcePosition;
            ResetComponents(previewSource);
        }
    }

    private void ResetComponents(GameObject obj)
    {
        var health = obj.GetComponent<HealthComponent>();
        if (health != null)
        {
            health.CurrentHealth = health.MaxHealth;
        }

        var shield = obj.GetComponent<ShieldComponent>();
        if (shield != null)
        {
            shield.CurrentShield = shield.MaxShield;
        }

        var energy = obj.GetComponent<EnergyComponent>();
        if (energy != null)
        {
            energy.CurrentEnergy = energy.MaxEnergy;
        }
    }

    private void ApplyEffect()
    {
        if (selectedEffect == null || previewTarget == null || previewSource == null) return;

        // 创建效果实体
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;

        var effectEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(effectEntity, new EffectComponent
        {
            Source = previewSource.GetComponent<NetworkEntity>().NetworkId,
            Target = previewTarget.GetComponent<NetworkEntity>().NetworkId,
            EffectData = selectedEffect,
            StartTime = Time.time
        });

        // 设置时间缩放
        Time.timeScale = timeScale;
    }

    private void OnSceneGUI()
    {
        if (!showGizmos || selectedEffect == null) return;

        // 绘制效果范围
        switch (selectedEffect.Type)
        {
            case EffectType.Chain:
                DrawChainGizmos();
                break;
            case EffectType.Area:
                DrawAreaGizmos();
                break;
        }
    }

    private void DrawChainGizmos()
    {
        if (previewTarget == null || previewSource == null) return;

        Handles.color = Color.yellow;
        Handles.DrawWireDisc(previewTarget.transform.position, Vector3.up, selectedEffect.ChainRange);

        // 绘制可能的链式目标位置
        for (int i = 0; i < selectedEffect.MaxChainTargets; i++)
        {
            float angle = (i + 1) * 90f * Mathf.Deg2Rad;
            Vector3 targetPos = previewTarget.transform.position + new Vector3(
                Mathf.Cos(angle) * selectedEffect.ChainRange,
                0f,
                Mathf.Sin(angle) * selectedEffect.ChainRange
            );
            Handles.DrawWireDisc(targetPos, Vector3.up, 0.5f);
        }
    }

    private void DrawAreaGizmos()
    {
        if (previewTarget == null) return;

        Handles.color = new Color(1f, 0f, 0f, 0.2f);
        Handles.DrawSolidDisc(previewTarget.transform.position, Vector3.up, selectedEffect.AreaRadius);
        Handles.color = Color.red;
        Handles.DrawWireDisc(previewTarget.transform.position, Vector3.up, selectedEffect.AreaRadius);
    }
} 