using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProfilerDataExporter
{
    /// <summary>
    /// Wrapper for unity internal SplitterState class.
    ///
    /// Unity has repeatedly changed this internal class's field types (int[] vs List&lt;int&gt; vs
    /// float[] for realSizes/relativeSizes, int vs float for splitSize/splitterInitialOffset) and
    /// method signatures (RealToRelativeSizes gained totalSpace/scale parameters) across Editor
    /// versions. Every accessor below is written defensively so it keeps working regardless of
    /// which shape the currently running Editor uses.
    /// </summary>
    public class SplitterState
    {
        private static readonly Type SplitterStateType = typeof(Editor).Assembly.GetType("UnityEditor.SplitterState");
        public object splitter = null;

        private static readonly FieldInfo RealSizesInfo = GetSplitterField("realSizes");
        private static readonly FieldInfo RelativeSizesInfo = GetSplitterField("relativeSizes");
        private static readonly FieldInfo IDInfo = GetSplitterField("ID");
        private static readonly FieldInfo XOffsetInfo = GetSplitterField("xOffset");
        private static readonly FieldInfo SplitSizeInfo = GetSplitterField("splitSize");
        private static readonly FieldInfo SplitterInitialOffsetInfo = GetSplitterField("splitterInitialOffset");
        private static readonly FieldInfo CurrentActiveSplitterInfo = GetSplitterField("currentActiveSplitter");

        private static readonly MethodInfo RealToRelativeSizesInfo = GetSplitterMethod("RealToRelativeSizes");
        private static readonly MethodInfo DoSplitterInfo = GetSplitterMethod("DoSplitter");

        private static FieldInfo GetSplitterField(string name)
        {
            return SplitterStateType.GetField(name,
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static MethodInfo GetSplitterMethod(string name)
        {
            return SplitterStateType.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public SplitterState(float[] relativeSizes, int[] minSizes, int[] maxSizes)
        {
            splitter = SplitterStateType.InvokeMember(null,
            BindingFlags.DeclaredOnly |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.CreateInstance, null, null, new object[] { relativeSizes, minSizes, maxSizes });
        }

        public SplitterState(object splitter)
        {
            this.splitter = splitter;
        }

        // --- Numeric conversion helpers -------------------------------------------------------
        // Unity has used both plain arrays and List<T> for realSizes/relativeSizes across
        // versions, and both int and float for the scalar fields. Convert.ToInt32/ToSingle handle
        // any boxed numeric type without an invalid-cast exception, unlike a direct (int)/(float)
        // unboxing cast, which throws unless the boxed type matches exactly.
        private static int[] ToIntArray(object value)
        {
            if (value == null) return new int[0];
            if (value is int[] array) return array;
            if (value is List<int> list) return list.ToArray();
            var result = new List<int>();
            foreach (var item in (IEnumerable)value) result.Add(Convert.ToInt32(item));
            return result.ToArray();
        }

        private static float[] ToFloatArray(object value)
        {
            if (value == null) return new float[0];
            if (value is float[] array) return array;
            if (value is List<float> list) return list.ToArray();
            var result = new List<float>();
            foreach (var item in (IEnumerable)value) result.Add(Convert.ToSingle(item));
            return result.ToArray();
        }

        // FIX (auditoría 15 ago 2026, confirmado contra Editor.log/Editor-prev.log): en Unity
        // 6000.5.4f1 la lectura de "splitSize" via reflection dispara InvalidCastException aquí
        // (Convert.ToInt32 no puede convertir el valor real del campo interno de Unity en esta
        // versión — la propia clase ya avisa en su docstring de que Unity cambia esta forma sin
        // avisar). Antes esto reventaba DrawStats() en cada repintado de la ventana, lo que a su
        // vez impedía llegar a DrawExportButtons() — por eso "Profiler Data Exporter" nunca
        // llegaba a exportar nada real (profiler_data.json se quedaba en su stub vacío). Igual que
        // el resto de la clase, se degrada a un valor por defecto en vez de propagar la excepción.
        private int GetInt(FieldInfo field)
        {
            try { return Convert.ToInt32(field.GetValue(splitter)); }
            catch (InvalidCastException) { return 0; }
        }

        private float GetFloat(FieldInfo field)
        {
            try { return Convert.ToSingle(field.GetValue(splitter)); }
            catch (InvalidCastException) { return 0f; }
        }

        private void SetNumeric(FieldInfo field, double value)
        {
            object converted = field.FieldType == typeof(float)
                ? (object)(float)value
                : field.FieldType == typeof(int)
                    ? (object)(int)value
                    : Convert.ChangeType(value, field.FieldType);
            field.SetValue(splitter, converted);
        }

        // Calls a reflected method, adapting the arguments we'd *like* to pass to whatever
        // parameter list the running Editor's method actually has (extra args we supply are
        // dropped, missing ones are defaulted), so signature changes between Editor versions
        // (e.g. RealToRelativeSizes gaining totalSpace/scale parameters) don't throw.
        private object InvokeAdaptive(MethodInfo method, params object[] preferredArgs)
        {
            if (method == null) return null;
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                object candidate = i < preferredArgs.Length ? preferredArgs[i] : DefaultForType(paramType);
                args[i] = paramType.IsInstanceOfType(candidate) ? candidate : Convert.ChangeType(candidate, paramType);
            }
            return method.Invoke(splitter, args);
        }

        private static object DefaultForType(Type t)
        {
            if (t == typeof(float)) return 0f;
            if (t == typeof(int)) return 0;
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }

        public int[] realSizes
        {
            get { return ToIntArray(RealSizesInfo.GetValue(splitter)); }
        }

        public float[] relativeSizes
        {
            get { return ToFloatArray(RelativeSizesInfo.GetValue(splitter)); }
        }

        public int ID
        {
            get { return GetInt(IDInfo); }
            internal set { SetNumeric(IDInfo, value); }
        }

        public float xOffset
        {
            get { return GetFloat(XOffsetInfo); }
        }

        public int splitSize
        {
            get { return GetInt(SplitSizeInfo); }
        }

        public int splitterInitialOffset
        {
            get { return GetInt(SplitterInitialOffsetInfo); }
            internal set { SetNumeric(SplitterInitialOffsetInfo, value); }
        }

        public int currentActiveSplitter
        {
            get { return GetInt(CurrentActiveSplitterInfo); }
            internal set { SetNumeric(CurrentActiveSplitterInfo, value); }
        }

        // totalSpace/scale are only used if the running Editor's RealToRelativeSizes expects them;
        // older Editor versions with a parameterless overload simply ignore the extra arguments.
        public void RealToRelativeSizes(float totalSpace, float scale)
        {
            InvokeAdaptive(RealToRelativeSizesInfo, totalSpace, scale);
        }

        public void DoSplitter(int currentActiveSplitter, int v, int num3)
        {
            InvokeAdaptive(DoSplitterInfo, currentActiveSplitter, v, num3);
        }
    }
}
