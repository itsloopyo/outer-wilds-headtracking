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
        /// <returns>True if listener was added successfully.</returns>
        public static bool AddListener(string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            return ManageListener("AddListener", eventName, targetTypeName, handlerMethodName, target);
        }

        /// <summary>
        /// Removes a listener from a GlobalMessenger&lt;T&gt; using reflection.
        /// </summary>
        /// <param name="eventName">The event name (e.g., "EnterSignalscopeZoom").</param>
        /// <param name="targetTypeName">The generic type parameter name (e.g., "Signalscope").</param>
        /// <param name="handlerMethodName">The handler method name on the target instance.</param>
        /// <param name="target">The target instance containing the handler method.</param>
        /// <returns>True if listener was removed successfully.</returns>
        public static bool RemoveListener(string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            return ManageListener("RemoveListener", eventName, targetTypeName, handlerMethodName, target);
        }

        private static bool ManageListener(string messengerMethodName, string eventName, string targetTypeName, string handlerMethodName, object target)
        {
            var globalMessengerType = AccessTools.TypeByName("GlobalMessenger`1");
            if (globalMessengerType == null) return false;

            var parameterType = AccessTools.TypeByName(targetTypeName);
            if (parameterType == null) return false;

            var messengerType = globalMessengerType.MakeGenericType(parameterType);
            var messengerMethod = AccessTools.Method(messengerType, messengerMethodName);
            if (messengerMethod == null) return false;

            // GlobalMessenger uses Callback<T> delegate, not Action<T>
            var callbackType = AccessTools.TypeByName("Callback`1");
            if (callbackType == null) return false;

            var delegateType = callbackType.MakeGenericType(parameterType);
            var handlerMethod = AccessTools.Method(target.GetType(), handlerMethodName, new Type[] { parameterType });
            if (handlerMethod == null) return false;

            var handlerDelegate = Delegate.CreateDelegate(delegateType, target, handlerMethod);
            messengerMethod.Invoke(null, new object[] { eventName, handlerDelegate });

            return true;
        }
    }
}
