using System;
using System.Collections.Generic;

namespace CatCraft
{
    public static class Triggers
    {
        private static Dictionary<string, bool> _activeTriggers = new Dictionary<string, bool>();
        private static Dictionary<string, Action<string>> _triggerCallbacks = new Dictionary<string, Action<string>>();

        public static void Reset()
        {
            _activeTriggers.Clear();
        }

        public static void RegisterCallback(string triggerPrefix, Action<string> callback)
        {
            if (!_triggerCallbacks.ContainsKey(triggerPrefix))
            {
                _triggerCallbacks[triggerPrefix] = callback;
            }
        }

        public static void Trigger(string triggerId)
        {
            _activeTriggers[triggerId] = true;

            string prefix = GetPrefix(triggerId);
            if (_triggerCallbacks.TryGetValue(prefix, out var callback))
            {
                callback(triggerId);
            }
        }

        public static bool IsActive(string triggerId)
        {
            return _activeTriggers.ContainsKey(triggerId);
        }

        public static void SetActive(string triggerId, bool active)
        {
            if (active)
            {
                _activeTriggers[triggerId] = true;
            }
            else
            {
                _activeTriggers.Remove(triggerId);
            }
        }

        public static Dictionary<string, bool> GetAllTriggers()
        {
            return new Dictionary<string, bool>(_activeTriggers);
        }

        public static void LoadTriggers(Dictionary<string, bool> savedTriggers)
        {
            _activeTriggers = new Dictionary<string, bool>(savedTriggers);
        }

        private static string GetPrefix(string triggerId)
        {
            int dotIndex = triggerId.IndexOf('.');
            return dotIndex > 0 ? triggerId.Substring(0, dotIndex) : triggerId;
        }
    }
}