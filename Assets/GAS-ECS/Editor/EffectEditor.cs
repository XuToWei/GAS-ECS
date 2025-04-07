using UnityEngine;
using UnityEditor;
using GAS.Effects;

public class EffectEditor : EditorWindow
{
    private string effectName = "";
    private EffectType effectType = EffectType.Instant;
    private float magnitude = 0f;
    private string[] selectedTags = new string[0];
    private string[] availableTags = new string[] { "Damage", "Heal", "Speed", "Shield", "Energy" };
    private float duration = 0f;
    private int priority = 0;
    private float chainRange = 0f;
    private int maxChainTargets = 0;
    private float areaRadius = 0f;
    private float damageReduction = 0f;
    private Vector2 scrollPosition;

    [MenuItem("GAS/Effect Editor")]
    public static void ShowWindow()
    {
        GetWindow<EffectEditor>("Effect Editor");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Effect Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 基本信息
        effectName = EditorGUILayout.TextField("Effect Name", effectName);
        effectType = (EffectType)EditorGUILayout.EnumPopup("Effect Type", effectType);
        magnitude = EditorGUILayout.FloatField("Magnitude", magnitude);

        // 标签选择
        EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
        for (int i = 0; i < availableTags.Length; i++)
        {
            bool isSelected = System.Array.Exists(selectedTags, tag => tag == availableTags[i]);
            bool newValue = EditorGUILayout.Toggle(availableTags[i], isSelected);
            if (newValue != isSelected)
            {
                if (newValue)
                {
                    System.Array.Resize(ref selectedTags, selectedTags.Length + 1);
                    selectedTags[selectedTags.Length - 1] = availableTags[i];
                }
                else
                {
                    selectedTags = System.Array.FindAll(selectedTags, tag => tag != availableTags[i]);
                }
            }
        }

        // 高级设置
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
        
        if (effectType == EffectType.Duration || effectType == EffectType.Periodic)
        {
            duration = EditorGUILayout.FloatField("Duration", duration);
        }
        
        priority = EditorGUILayout.IntField("Priority", priority);
        
        if (effectType == EffectType.Chain)
        {
            chainRange = EditorGUILayout.FloatField("Chain Range", chainRange);
            maxChainTargets = EditorGUILayout.IntField("Max Chain Targets", maxChainTargets);
        }
        
        if (effectType == EffectType.Area)
        {
            areaRadius = EditorGUILayout.FloatField("Area Radius", areaRadius);
            damageReduction = EditorGUILayout.FloatField("Damage Reduction", damageReduction);
        }

        // 预览
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        DrawPreview();

        // 创建按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Create Effect"))
        {
            CreateEffect();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPreview()
    {
        Rect previewRect = GUILayoutUtility.GetRect(200, 200);
        EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));

        switch (effectType)
        {
            case EffectType.Instant:
                DrawInstantPreview(previewRect);
                break;
            case EffectType.Duration:
                DrawDurationPreview(previewRect);
                break;
            case EffectType.Periodic:
                DrawPeriodicPreview(previewRect);
                break;
            case EffectType.Chain:
                DrawChainPreview(previewRect);
                break;
            case EffectType.Area:
                DrawAreaPreview(previewRect);
                break;
        }
    }

    private void DrawInstantPreview(Rect rect)
    {
        float centerX = rect.x + rect.width / 2;
        float centerY = rect.y + rect.height / 2;
        float radius = 20f;

        Handles.color = Color.red;
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);
    }

    private void DrawDurationPreview(Rect rect)
    {
        float centerX = rect.x + rect.width / 2;
        float centerY = rect.y + rect.height / 2;
        float radius = 20f;

        Handles.color = Color.green;
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);
        
        // 绘制持续时间指示器
        float progress = Mathf.PingPong(Time.time, 1f);
        float angle = progress * 360f;
        Vector3 endPoint = new Vector3(
            centerX + Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
            centerY + Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
            0
        );
        Handles.DrawLine(new Vector3(centerX, centerY, 0), endPoint);
    }

    private void DrawPeriodicPreview(Rect rect)
    {
        float centerX = rect.x + rect.width / 2;
        float centerY = rect.y + rect.height / 2;
        float radius = 20f;

        Handles.color = Color.blue;
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);
        
        // 绘制周期性效果指示器
        float time = Time.time * 2f;
        for (int i = 0; i < 3; i++)
        {
            float angle = (time + i * 120f) * Mathf.Deg2Rad;
            Vector3 point = new Vector3(
                centerX + Mathf.Cos(angle) * radius,
                centerY + Mathf.Sin(angle) * radius,
                0
            );
            Handles.DrawSolidDisc(point, Vector3.forward, 5f);
        }
    }

    private void DrawChainPreview(Rect rect)
    {
        float centerX = rect.x + rect.width / 2;
        float centerY = rect.y + rect.height / 2;
        float radius = 20f;

        // 绘制主目标
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);

        // 绘制链式目标
        int targetCount = Mathf.Min(maxChainTargets, 3);
        for (int i = 0; i < targetCount; i++)
        {
            float angle = (i + 1) * 90f * Mathf.Deg2Rad;
            Vector3 targetPos = new Vector3(
                centerX + Mathf.Cos(angle) * chainRange,
                centerY + Mathf.Sin(angle) * chainRange,
                0
            );
            Handles.DrawWireDisc(targetPos, Vector3.forward, radius * 0.8f);
            Handles.DrawLine(new Vector3(centerX, centerY, 0), targetPos);
        }
    }

    private void DrawAreaPreview(Rect rect)
    {
        float centerX = rect.x + rect.width / 2;
        float centerY = rect.y + rect.height / 2;
        float radius = areaRadius > 0 ? areaRadius : 30f;

        // 绘制区域范围
        Handles.color = new Color(1f, 0f, 0f, 0.2f);
        Handles.DrawSolidDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);
        
        // 绘制中心点
        Handles.color = Color.red;
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, 10f);
    }

    private void CreateEffect()
    {
        if (string.IsNullOrEmpty(effectName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter an effect name", "OK");
            return;
        }

        if (magnitude == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please enter a non-zero magnitude", "OK");
            return;
        }

        // 创建效果资源
        var effect = ScriptableObject.CreateInstance<EffectData>();
        effect.Name = effectName;
        effect.Type = effectType;
        effect.Magnitude = magnitude;
        effect.Tags = selectedTags;
        effect.Duration = duration;
        effect.Priority = priority;
        effect.ChainRange = chainRange;
        effect.MaxChainTargets = maxChainTargets;
        effect.AreaRadius = areaRadius;
        effect.DamageReduction = damageReduction;

        // 保存资源
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Effect",
            effectName,
            "asset",
            "Choose location to save effect"
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(effect, path);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Effect created successfully!", "OK");
        }
    }
} 