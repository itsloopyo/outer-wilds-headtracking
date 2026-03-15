extern alias OWMLCommon;
extern alias UnityCoreModule;
using System.Collections.Generic;
using IModHelper = OWMLCommon::OWML.Common.IModHelper;
using Transform = UnityCoreModule::UnityEngine.Transform;
using Renderer = UnityCoreModule::UnityEngine.Renderer;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.Effects
{
    public static class PlayerHeadHider
    {
        public static void LogNearbyRenderers(Transform cameraTransform, IModHelper modHelper)
        {
            var root = cameraTransform;
            while (root.parent != null) root = root.parent;

            var cameraPos = cameraTransform.position;
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            modHelper.Console.WriteLine(
                $"[HeadHider] Camera at {cameraPos}, scanning {renderers.Length} renderers under '{root.name}'");

            foreach (var r in renderers)
            {
                float dist = Vector3.Distance(r.bounds.center, cameraPos);
                string path = GetHierarchyPath(r.transform, root);
                string type = r.GetType().Name;
                modHelper.Console.WriteLine(
                    $"[HeadHider] dist={dist:F3} type={type} enabled={r.enabled} path={path}");
            }
        }

        private static string GetHierarchyPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            parts.Insert(0, root.name);
            return string.Join("/", parts);
        }
    }
}
