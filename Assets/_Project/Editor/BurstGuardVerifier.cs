using System;
using System.Reflection;
using System.Text;
using Unity.Burst;
using UnityEditor;
using UnityEngine;

namespace Cinder.EditorTools
{
    /// <summary>
    /// 静态检查所有 [BurstCompile] 类型是否符合 Burst 规约。
    /// 每次新增 Job 之后点一下：菜单 Cinder → Verify Burst Compliance。
    /// </summary>
    public static class BurstGuardVerifier
    {
        [MenuItem("Cinder/Verify Burst Compliance", false, 100)]
        public static void Verify()
        {
            var sb = new StringBuilder();
            int checkedCount = 0;
            int violations = 0;

            foreach (Type t in TypeCache.GetTypesWithAttribute<BurstCompileAttribute>())
            {
                // 只检查我们自己的程序集，Unity 官方包内部类型不归本工具管
                if (t.Namespace == null || !t.Namespace.StartsWith("Cinder")) continue;

                checkedCount++;

                // 规约 1：必须是 struct。class 会被 Burst 直接忽略。
                if (!t.IsValueType)
                {
                    sb.AppendLine($"[类型] {t.FullName} 不是 struct，Burst 不会编译它。");
                    violations++;
                    continue;
                }

                // 规约 2：字段不能是接口、委托或托管引用类型。
                FieldInfo[] fields = t.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (FieldInfo f in fields)
                {
                    Type ft = f.FieldType;

                    if (ft.IsInterface)
                    {
                        sb.AppendLine($"[字段] {t.Name}.{f.Name} 是接口 {ft.Name}，需要虚函数表，Burst 拒编。");
                        violations++;
                    }
                    else if (typeof(Delegate).IsAssignableFrom(ft))
                    {
                        sb.AppendLine($"[字段] {t.Name}.{f.Name} 是委托 {ft.Name}。改成写队列、主线程消费。");
                        violations++;
                    }
                    else if (!ft.IsValueType && ft != typeof(string))
                    {
                        sb.AppendLine($"[字段] {t.Name}.{f.Name} 是托管类型 {ft.Name}。只能用 NativeArray 等原生容器。");
                        violations++;
                    }
                }

                // 规约 3：开发期应开 CompileSynchronously，否则异步编译失败你看不到。
                var attr = t.GetCustomAttribute<BurstCompileAttribute>();
                if (attr != null && !attr.CompileSynchronously)
                {
                    sb.AppendLine($"[建议] {t.Name} 未开 CompileSynchronously，"
                                + "异步编译失败不会立即可见。发布前再关。");
                }
            }

            if (violations == 0)
                Debug.Log($"[Cinder] Burst 规约检查通过。己检查 {checkedCount} 个类型。\n{sb}");
            else
                Debug.LogError($"[Cinder] 发现 {violations} 处违规"
                             + $"（己检查 {checkedCount} 个类型）：\n{sb}");
        }
    }
}
