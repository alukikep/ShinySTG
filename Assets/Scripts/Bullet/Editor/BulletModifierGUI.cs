using System;
using System.Collections.Generic;
using System.Reflection;
using SerializeReferenceEditor.Editor;
using SerializeReferenceEditor.Editor.Drawers;
using SerializeReferenceEditor.Editor.Services;
using SerializeReferenceEditor.Editor.SRActions;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>Shared layout for root modifiers and container phases.</summary>
internal static class BulletModifierGUI
{
    static readonly SRCashTypeSearchTree _typeTrees = new();
    static readonly NameService _names = new();
    static float Line => EditorGUIUtility.singleLineHeight;
    static float Gap => EditorGUIUtility.standardVerticalSpacing;

    public static float GetHeight(SerializedProperty property)
        => Line + (property.isExpanded ? GetBodyHeight(property, IsPhaseChild(property)) : 0f);

    public static float GetBodyHeight(SerializedProperty property, bool phaseChild)
        => LayoutBody(new Rect(), property, phaseChild, false);

    public static void Draw(Rect position, SerializedProperty property, GUIContent label)
    {
        int previousIndent = EditorGUI.indentLevel;
        try
        {
            Rect row = EditorGUI.IndentedRect(new Rect(position.x, position.y, position.width, Line));
            EditorGUI.indentLevel = 0;
            Rect typeRect = new Rect(row.xMax - 24f, row.y, 24f, Line);
            Rect titleRect = new Rect(row.x, row.y, Mathf.Max(20f, row.width - 30f), Line);
            string title = GetTypeName(property);
            string number = GetElementNumber(property);
            if (number.Length > 0) title = number + " · " + title;
            else if (label != null && label.text != "Modifier") title = label.text + " · " + title;
            string summary = GetTimingSummary(property, IsPhaseChild(property));
            if (summary.Length > 0) title += " · " + summary;
            property.isExpanded = EditorGUI.Foldout(titleRect, property.isExpanded,
                new GUIContent(FitTitle(title, titleRect.width), title), true);
            DrawTypeButton(typeRect, property, typeof(BulletModifier));
            if (property.isExpanded)
                DrawBody(new Rect(row.x + 12f, row.yMax, Mathf.Max(0f, row.width - 12f),
                    position.height - Line), property, IsPhaseChild(property));
        }
        finally
        {
            EditorGUI.indentLevel = previousIndent;
        }
    }

    public static void DrawBody(Rect rect, SerializedProperty property, bool phaseChild)
    {
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        try
        {
            EditorGUIUtility.labelWidth = Mathf.Min(previousLabelWidth, Mathf.Max(60f, rect.width * 0.48f));
            LayoutBody(rect, property, phaseChild, true);
        }
        finally
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }
    }

    public static string FitTitle(string title, float width)
    {
        float available = Mathf.Max(0f, width - 18f);
        if (EditorStyles.foldout.CalcSize(new GUIContent(title)).x <= available) return title;
        while (title.Length > 0 && EditorStyles.foldout.CalcSize(new GUIContent(title + "…")).x > available)
            title = title.Substring(0, title.Length - 1);
        return title + "…";
    }

    static float LayoutBody(Rect rect, SerializedProperty property, bool phaseChild, bool draw)
    {
        if (property == null) return 0f;
        // Different managed-reference types must be edited individually to avoid invalid child paths.
        if (HasMixedTypes(property))
        {
            if (draw) EditorGUI.LabelField(new Rect(rect.x, rect.y + Gap, rect.width, Line), "不同类型请分别编辑");
            return Line + Gap;
        }
        if (string.IsNullOrEmpty(property.managedReferenceFullTypename)) return 0f;
        var type = SRTypeCache.GetTypeByName(property.managedReferenceFullTypename);
        if (type == typeof(ClearDefenseBulletModifier))
        {
            if (draw) EditorGUI.LabelField(new Rect(rect.x, rect.y + Gap, rect.width, Line),
                new GUIContent("抵挡一级消弹；二级仍可消除。", "挂载期间常驻，首帧和雾化期生效；不受 Timing 设置影响。容器子节点在阶段开始时生效，结束时解除。"));
            return Line + Gap;
        }
        float y = 0f;
        if (!phaseChild)
        {
            var trigger = property.FindPropertyRelative(nameof(BulletModifier.StartTrigger));
            ReferenceField(rect, ref y, trigger, "启动条件", typeof(ModifierStartTrigger), draw);
            var oneShot = property.FindPropertyRelative(nameof(BulletModifier.OneShot));
            if (oneShot.hasMultipleDifferentValues || !oneShot.boolValue)
                Field(rect, ref y, property.FindPropertyRelative(nameof(BulletModifier.Duration)),
                    new GUIContent("生效时长 (秒)", "从启动后计算；<= 0 表示无限。雾化期间不推进此时间。"), type, draw);
        }

        // Enumerate only immediate visible fields: nested references keep their own drawers.
        foreach (var child in GetBehaviorFields(property, type))
        {
            if (type == typeof(HomingEnemyModifier) && child.name == nameof(HomingEnemyModifier.LockOnDelay))
            {
                Rect heading = Next(rect, ref y, Line);
                if (draw) EditorGUI.LabelField(heading, "追踪时间 · 从行为开始后累计", EditorStyles.miniLabel);
            }
            if (child.name == nameof(SequenceBulletModifier.Entries))
            {
                Field(rect, ref y, child, new GUIContent("阶段列表", "Sequence 顺序执行，Parallel 同时执行。每项的阶段时长由容器控制。"), type, draw);
                continue;
            }
            Field(rect, ref y, child, GetLabel(child, type), type, draw);
        }

        if (!phaseChild)
        {
            var oneShot = property.FindPropertyRelative(nameof(BulletModifier.OneShot));
            var skip = property.FindPropertyRelative(nameof(BulletModifier.AutoSkipOutsideWindow));
            bool nonDefault = oneShot.boolValue != (type == typeof(FireOnEnterBulletModifier) || type == typeof(AimAtPlayerModifier))
                || !skip.boolValue || oneShot.hasMultipleDifferentValues || skip.hasMultipleDifferentValues;
            string key = StateKey(property);
            bool expanded = SessionState.GetBool(key, false);
            Rect advancedRect = Next(rect, ref y, Line);
            if (draw)
            {
                expanded = EditorGUI.Foldout(advancedRect, expanded,
                    nonDefault ? "高级设置 · 非默认" : "高级设置", true);
                SessionState.SetBool(key, expanded);
            }
            if (expanded)
            {
                Field(rect, ref y, oneShot, new GUIContent("一次触发", "仅执行进入和退出回调，不逐帧执行行为。此选项需要行为自身支持。"), type, draw);
                if (oneShot.hasMultipleDifferentValues || !oneShot.boolValue)
                    Field(rect, ref y, skip, new GUIContent("窗口外停用", "关闭后会在窗口外继续调用行为；仅供支持此机制的自定义行为使用。"), type, draw);
            }
        }
        return y;
    }

    internal static IEnumerable<SerializedProperty> GetBehaviorFields(SerializedProperty property, Type type)
    {
        var child = property.Copy();
        var end = property.GetEndProperty();
        if (!child.NextVisible(true)) yield break;
        do
        {
            if (SerializedProperty.EqualContents(child, end) || child.depth <= property.depth) yield break;
            if (child.depth == property.depth + 1 && IsVisible(property, child.name, type)) yield return child.Copy();
        }
        while (child.NextVisible(false));
    }

    static bool IsVisible(SerializedProperty property, string name, Type type)
    {
        if (name == nameof(BulletModifier.StartTrigger) || name == nameof(BulletModifier.Duration)
            || name == nameof(BulletModifier.OneShot) || name == nameof(BulletModifier.AutoSkipOutsideWindow)) return false;
        if (type == typeof(OrbitBulletModifier))
        {
            if (name == nameof(OrbitBulletModifier.FixedCenter))
                return Matches(property, nameof(OrbitBulletModifier.Mode), (int)OrbitBulletModifier.CenterMode.FixedPosition);
            if (name == nameof(OrbitBulletModifier.Radius))
                return Matches(property, nameof(OrbitBulletModifier.RadiusBehavior), (int)OrbitBulletModifier.RadiusMode.Constant);
            if (name == nameof(OrbitBulletModifier.TargetRadius) || name == nameof(OrbitBulletModifier.RadiusDuration))
                return Matches(property, nameof(OrbitBulletModifier.RadiusBehavior), (int)OrbitBulletModifier.RadiusMode.Linear,
                    (int)OrbitBulletModifier.RadiusMode.Curve);
            if (name == nameof(OrbitBulletModifier.RadiusCurve))
                return Matches(property, nameof(OrbitBulletModifier.RadiusBehavior), (int)OrbitBulletModifier.RadiusMode.Curve);
        }
        if (type != null && typeof(BulletColorModifier).IsAssignableFrom(type))
        {
            if (name == "AutoSetDrawMode") return false; // Legacy serialized field has no runtime use.
            if (name == nameof(BulletColorModifier.FadeOutColor) || name == nameof(BulletColorModifier.FadeStart)
                || name == nameof(BulletColorModifier.ReferenceLifetime))
                return Matches(property, nameof(BulletColorModifier.Mode), (int)BulletColorModifier.ColorMode.FadeByLifetime);
            if (name == nameof(BulletColorModifier.FlashFrequency) || name == "FlashMinAlpha")
                return Matches(property, nameof(BulletColorModifier.Mode), (int)BulletColorModifier.ColorMode.Flash);
        }
        if (type == typeof(ParallelBulletModifier) && name == nameof(ParallelBulletModifier.CycleDuration))
        {
            var loop = property.FindPropertyRelative(nameof(ParallelBulletModifier.Loop));
            return loop.hasMultipleDifferentValues || loop.boolValue;
        }
        return true;
    }

    static bool Matches(SerializedProperty property, string name, params int[] values)
    {
        var mode = property.FindPropertyRelative(name);
        if (mode.hasMultipleDifferentValues) return true;
        return Array.IndexOf(values, mode.intValue) >= 0;
    }

    static GUIContent GetLabel(SerializedProperty property, Type type)
    {
        string text = property.displayName;
        string tooltip = property.tooltip;
        switch (property.name)
        {
            case "RadiusDuration": text = "半径变化时长 (秒)"; break;
            case "CycleDuration": text = "循环周期 (秒)"; break;
            case "LockOnDelay": text = "追踪开始延迟 (秒)"; break;
            case "MaxHomingTime": text = "追踪时长 (秒)"; break;
            case "ReferenceLifetime": text = "渐变参考时长 (秒)"; break;
            case "FixedCenter": text = "固定圆心"; break;
            case "AngularSpeed": text = "角速度 (度/秒)"; break;
            case "RadiusBehavior": text = "半径模式"; break;
            case "Radius": text = "固定半径"; break;
            case "TargetRadius": text = "目标半径"; break;
            case "RadiusCurve": text = "半径曲线"; break;
            case "MaxCycles": text = "循环次数 (0 = 无限)"; break;
            case "Loop": text = "循环"; break;
        }
        if (type != null && typeof(HomingEnemyModifier).IsAssignableFrom(type)
            && (property.name == "LockOnDelay" || property.name == "MaxHomingTime"))
            tooltip = "从行为开始执行后累计，与外层生效时长独立。" + tooltip;
        return new GUIContent(text, tooltip);
    }

    static void Field(Rect rect, ref float y, SerializedProperty property, GUIContent label, Type ownerType, bool draw)
    {
        var field = ownerType?.GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        bool scalar = IsScalar(property.propertyType);
        Rect row = Next(rect, ref y, scalar ? Line : EditorGUI.GetPropertyHeight(property, label, true));
        if (!draw) return;
        EditorGUI.BeginProperty(row, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        try
        {
            // Draw primitive fields without inherited Header decorators; retain ranges and min constraints.
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    var range = field?.GetCustomAttribute<RangeAttribute>();
                    var min = field?.GetCustomAttribute<MinAttribute>();
                    float value = range != null ? EditorGUI.Slider(row, label, property.floatValue, range.min, range.max)
                        : EditorGUI.FloatField(row, label, property.floatValue);
                    if (EditorGUI.EndChangeCheck()) property.floatValue = min != null ? Mathf.Max(min.min, value) : value;
                    return;
                case SerializedPropertyType.Integer:
                    int intValue = EditorGUI.IntField(row, label, property.intValue);
                    var intMin = field?.GetCustomAttribute<MinAttribute>();
                    if (EditorGUI.EndChangeCheck()) property.intValue = intMin != null ? Mathf.Max(Mathf.CeilToInt(intMin.min), intValue) : intValue;
                    return;
                case SerializedPropertyType.Boolean:
                    bool boolValue = EditorGUI.Toggle(row, label, property.boolValue);
                    if (EditorGUI.EndChangeCheck()) property.boolValue = boolValue;
                    return;
                case SerializedPropertyType.Enum:
                    int enumValue = EditorGUI.Popup(row, label, property.enumValueIndex,
                        Array.ConvertAll(property.enumDisplayNames, n => new GUIContent(n)));
                    if (EditorGUI.EndChangeCheck()) property.enumValueIndex = enumValue;
                    return;
                case SerializedPropertyType.Color:
                    Color color = EditorGUI.ColorField(row, label, property.colorValue);
                    if (EditorGUI.EndChangeCheck()) property.colorValue = color;
                    return;
                case SerializedPropertyType.AnimationCurve:
                    AnimationCurve curve = EditorGUI.CurveField(row, label, property.animationCurveValue);
                    if (EditorGUI.EndChangeCheck()) property.animationCurveValue = curve;
                    return;
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object reference = EditorGUI.ObjectField(row, label, property.objectReferenceValue,
                        field?.FieldType ?? typeof(UnityEngine.Object), false);
                    if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = reference;
                    return;
                case SerializedPropertyType.Vector2:
                    Vector2 vector = EditorGUI.Vector2Field(row, label, property.vector2Value);
                    if (EditorGUI.EndChangeCheck()) property.vector2Value = vector;
                    return;
                default:
                    EditorGUI.PropertyField(row, property, label, true);
                    EditorGUI.EndChangeCheck();
                    return;
            }
        }
        finally
        {
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
        }
    }

    static bool IsScalar(SerializedPropertyType type)
        => type == SerializedPropertyType.Float || type == SerializedPropertyType.Integer
            || type == SerializedPropertyType.Boolean || type == SerializedPropertyType.Enum
            || type == SerializedPropertyType.Color || type == SerializedPropertyType.AnimationCurve
            || type == SerializedPropertyType.ObjectReference;

    static void ReferenceField(Rect rect, ref float y, SerializedProperty property, string title, Type baseType, bool draw)
    {
        Rect row = Next(rect, ref y, Line);
        bool hasValue = !string.IsNullOrEmpty(property.managedReferenceFullTypename);
        if (draw)
        {
            Rect button = new Rect(row.xMax - 110f, row.y, 110f, Line);
            Rect label = new Rect(row.x, row.y, Mathf.Max(20f, row.width - 114f), Line);
            property.isExpanded = hasValue && EditorGUI.Foldout(label, property.isExpanded, title, true);
            DrawTypeButton(button, property, baseType, hasValue ? GetTypeName(property) : "立即启动");
        }
        if (hasValue && property.isExpanded && !HasMixedTypes(property))
        {
            var type = SRTypeCache.GetTypeByName(property.managedReferenceFullTypename);
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (child.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end) || child.depth <= property.depth) break;
                    Field(rect, ref y, child, new GUIContent(child.displayName, child.tooltip), type, draw);
                } while (child.NextVisible(false));
        }
    }

    public static void DrawTypeButton(Rect rect, SerializedProperty property, Type baseType, string title = "⋮")
    {
        if (property == null) return;
        using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
        {
            if (!EditorGUI.DropdownButton(rect, new GUIContent(title,
                    property.serializedObject.isEditingMultipleObjects ? "更换类型请单独选择一个资产。" : "搜索、更换类型或清空；更换类型会重置该行为配置。"), FocusType.Passive)) return;
            var types = SRTypeCache.GetTypeInfos(baseType);
            var actions = new SRActionFactory(property.Copy(), null, types);
            var search = SRTypesSearchWindowProvider.MakeTypesContainer(actions, _typeTrees.GetTypeTreeFactory(types));
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), search);
        }
    }

    static Rect Next(Rect rect, ref float y, float height)
    {
        y += Gap;
        var row = new Rect(rect.x, rect.y + y, rect.width, height);
        y += height;
        return row;
    }

    public static string GetTypeName(SerializedProperty property)
        => property == null ? "未选择行为" : HasMixedTypes(property) ? "混合类型"
            : string.IsNullOrEmpty(property.managedReferenceFullTypename) ? "未选择行为"
            : _names.GetTypeName(property.managedReferenceFullTypename);

    public static string GetElementNumber(SerializedProperty property)
    {
        string path = property.propertyPath;
        if (!path.EndsWith("]", StringComparison.Ordinal)) return string.Empty;
        int start = path.LastIndexOf('[');
        return int.TryParse(path.Substring(start + 1, path.Length - start - 2), out int index)
            ? (index + 1).ToString() : string.Empty;
    }

    static bool IsPhaseChild(SerializedProperty property)
    {
        // Only an immediate BulletModifierEntry parent controls this modifier's time.
        // ExtraModifiers create new bullets with their own time windows.
        if (!property.propertyPath.EndsWith(".Modifier", StringComparison.Ordinal)) return false;
        string parentPath = property.propertyPath.Substring(0, property.propertyPath.Length - ".Modifier".Length);
        return property.serializedObject.FindProperty(parentPath)?.type == nameof(BulletModifierEntry);
    }

    static bool HasMixedTypes(SerializedProperty property)
    {
        if (!property.serializedObject.isEditingMultipleObjects || !property.hasMultipleDifferentValues) return false;
        string typeName = null;
        bool first = true;
        foreach (var target in property.serializedObject.targetObjects)
        {
            using (var serialized = new SerializedObject(target))
            {
                var item = serialized.FindProperty(property.propertyPath);
                string currentType = item?.managedReferenceFullTypename;
                if (!first && currentType != typeName) return true;
                typeName = currentType;
                first = false;
            }
        }
        return false;
    }

    static string GetTimingSummary(SerializedProperty property, bool phaseChild)
    {
        if (phaseChild || string.IsNullOrEmpty(property.managedReferenceFullTypename) || HasMixedTypes(property)) return string.Empty;
        if (SRTypeCache.GetTypeByName(property.managedReferenceFullTypename) == typeof(ClearDefenseBulletModifier))
            return "挂载期间常驻";
        var trigger = property.FindPropertyRelative(nameof(BulletModifier.StartTrigger));
        string start = string.IsNullOrEmpty(trigger.managedReferenceFullTypename) ? "立即启动" : GetTypeName(trigger);
        var oneShot = property.FindPropertyRelative(nameof(BulletModifier.OneShot));
        var duration = property.FindPropertyRelative(nameof(BulletModifier.Duration));
        if (trigger.hasMultipleDifferentValues || oneShot.hasMultipleDifferentValues || duration.hasMultipleDifferentValues)
            return "时间设置不同";
        string end = oneShot.boolValue ? "一次触发" : duration.floatValue > 0f ? duration.floatValue.ToString("0.###") + " 秒" : "无限";
        bool advanced = !property.FindPropertyRelative(nameof(BulletModifier.AutoSkipOutsideWindow)).boolValue;
        return start + " · " + end + (advanced ? " · 窗口外运行" : string.Empty);
    }

    static string StateKey(SerializedProperty property)
        => "BulletModifier.Advanced." + property.serializedObject.targetObject.GetInstanceID() + "." + property.propertyPath;
}
