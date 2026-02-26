extern alias UnityCoreModule;
using System;
using HarmonyLib;

namespace OuterWildsHeadTracking.Utilities
{
    /// <summary>
    /// Provides fast field access using HarmonyLib's FieldRefAccess.
    /// This creates a delegate that directly accesses the field without reflection overhead.
    /// Performance: ~10x faster than FieldInfo.GetValue() due to no boxing/security checks.
    /// </summary>
    public static class FastFieldRef
    {
        /// <summary>
        /// Creates a fast field reference for instance fields.
        /// The returned delegate provides direct field access without reflection.
        /// </summary>
        /// <typeparam name="TTarget">The type containing the field.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>A delegate that reads the field value directly.</returns>
        public static AccessTools.FieldRef<TTarget, TField> Create<TTarget, TField>(string fieldName)
        {
            var fieldRef = AccessTools.FieldRefAccess<TTarget, TField>(fieldName);
            if (fieldRef == null)
            {
                throw new InvalidOperationException(
                    $"Could not create field reference for '{fieldName}' on type '{typeof(TTarget).Name}'");
            }
            return fieldRef;
        }
    }
}
