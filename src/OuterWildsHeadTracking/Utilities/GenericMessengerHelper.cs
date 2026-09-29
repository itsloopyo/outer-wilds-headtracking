using System;
using HarmonyLib;

namespace OuterWildsHeadTracking.Utilities
{
    /// <summary>
    /// Helper for registering listeners with generic GlobalMessenger types
    /// that aren't directly accessible (e.g., GlobalMessenger&lt;Signalscope&gt;).
    /// </summary>
    public static class GenericMessengerHelper
    {
        /// <summary>
        /// Adds a listener to a GlobalMessenger&lt;T&gt; using reflection.
        /// </summary>
        /// <param name="eventName">The event name (e.g., "EnterSignalscopeZoom").</param>
        /// <param name="targetTypeName">The generic type parameter name (e.g., "Signalscope").</param>
        /// <param name="handlerMethodName">The handler method name on the target instance.</param>
        /// <param name="target">The target instance containing the handler method.</param>
        public static void AddListener(string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            ManageListener("AddListener", eventName, targetTypeName, handlerMethodName, target);
        }

        /// <summary>
        /// Removes a listener from a GlobalMessenger&lt;T&gt; using reflection.
        /// </summary>
        /// <param name="eventName">The event name (e.g., "EnterSignalscopeZoom").</param>
        /// <param name="targetTypeName">The generic type parameter name (e.g., "Signalscope").</param>
        /// <param name="handlerMethodName">The handler method name on the target instance.</param>
        /// <param name="target">The target instance containing the handler method.</param>
        public static void RemoveListener(string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            ManageListener("RemoveListener", eventName, targetTypeName, handlerMethodName, target);
        }

        private static void ManageListener(string messengerMethodName, string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            var globalMessengerType = AccessTools.TypeByName("GlobalMessenger`1")
                ?? throw new InvalidOperationException("Could not find GlobalMessenger`1 type!");

            var parameterType = AccessTools.TypeByName(targetTypeName)
                ?? throw new InvalidOperationException($"Could not find {targetTypeName} type!");

            var messengerType = globalMessengerType.MakeGenericType(parameterType);
            var messengerMethod = AccessTools.Method(messengerType, messengerMethodName)
                ?? throw new InvalidOperationException($"Could not find GlobalMessenger<{targetTypeName}>.{messengerMethodName}!");

            // GlobalMessenger uses Callback<T> delegate, not Action<T>
            var callbackType = AccessTools.TypeByName("Callback`1")
                ?? throw new InvalidOperationException("Could not find Callback`1 type!");

            var delegateType = callbackType.MakeGenericType(parameterType);
            var handlerMethod = AccessTools.Method(target.GetType(), handlerMethodName, new Type[] { parameterType })
                ?? throw new InvalidOperationException($"Could not find {target.GetType().Name}.{handlerMethodName}!");

            var handlerDelegate = Delegate.CreateDelegate(delegateType, target, handlerMethod);
            messengerMethod.Invoke(null, new object[] { eventName, handlerDelegate });
        }
    }
}
