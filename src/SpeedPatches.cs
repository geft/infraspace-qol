using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InfraSpaceQoL
{
    internal static class SpeedPatches
    {
        private static int veryFastButtonIndex = -1;
        private const string VeryFastButtonName = "InfraSpaceQoL_VeryFastButton";

        internal static void DashboardStartPostfix(Dashboard __instance)
        {
            AddVeryFastButton(__instance);
        }

        internal static void DashboardStartAfterLoadPostfix(Dashboard __instance)
        {
            AddVeryFastButton(__instance);
        }

        private static void AddVeryFastButton(Dashboard __instance)
        {
            ToggleButtonGroup group = __instance.timeToggleButtonGroup;
            if (group == null)
            {
                Debug.LogWarning("[InfraSpaceQoL] Could not add the Very Fast button because the dashboard speed group was unavailable.");
                return;
            }

            List<ToggleButton> buttons = group.GetToggleButtons();
            if (buttons == null)
            {
                Debug.LogWarning("[InfraSpaceQoL] Could not add the Very Fast button because the game's speed buttons were unavailable.");
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == VeryFastButtonName)
                {
                    veryFastButtonIndex = i;
                    return;
                }
            }

            if (buttons.Count == 0)
            {
                Debug.LogWarning("[InfraSpaceQoL] Could not add the Very Fast button because there were no speed buttons to clone.");
                return;
            }

            GameObject buttonObject = null;
            ToggleButton veryFastButton = null;
            bool addedToGroup = false;
            int buttonIndex = buttons.Count;
            try
            {
                ToggleButton template = buttons[buttons.Count - 1];
                buttonObject = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
                buttonObject.name = VeryFastButtonName;
                buttonObject.SetActive(true);
                veryFastButton = buttonObject.GetComponent<ToggleButton>();
                if (veryFastButton == null)
                {
                    throw new InvalidOperationException("The cloned speed button did not contain a ToggleButton component.");
                }

                PlaceAfterExistingButtons(buttons, template, veryFastButton);
                buttons.Add(veryFastButton);
                addedToGroup = true;
                veryFastButtonIndex = buttonIndex;
                veryFastButton.SetToggleButtonGroup(group);
                veryFastButton.SetIsPressed(false, false);
                if (veryFastButton.text != null)
                {
                    veryFastButton.text.text = "4x";
                }

                TooltipHoverable tooltip = buttonObject.GetComponentInChildren<TooltipHoverable>(true);
                if (tooltip != null)
                {
                    // The clone carries the original x2 localization keys. Start() reapplies
                    // those keys later unless they are cleared before setting custom text.
                    tooltip.titleCode = string.Empty;
                    tooltip.descriptionCode = string.Empty;
                    tooltip.Init("Very Fast", "Game speed: 4x");
                }
                Debug.Log("[InfraSpaceQoL] Added the Very Fast (4x) dashboard speed button at index " + buttonIndex + ".");
            }
            catch (Exception exception)
            {
                if (addedToGroup)
                {
                    buttons.Remove(veryFastButton);
                }
                if (veryFastButtonIndex == buttonIndex)
                {
                    veryFastButtonIndex = -1;
                }
                if (buttonObject != null)
                {
                    UnityEngine.Object.Destroy(buttonObject);
                }
                Debug.LogError("[InfraSpaceQoL] Could not add the Very Fast speed button. " + exception);
            }
        }

        internal static bool TimeButtonSelectionPrefix(TimeManager __instance)
        {
            Dashboard dashboard = WorldScripts.Inst.dashboard;
            int buttonIndex = veryFastButtonIndex;
            if (buttonIndex < 0 || dashboard == null || dashboard.timeToggleButtonGroup == null ||
                dashboard.timeToggleButtonGroup.selectedButton != buttonIndex)
            {
                return true;
            }

            __instance.SetTimeState(TimeState.VeryFast, true, true);
            return false;
        }

        internal static void TimeStatePostfix(TimeManager __instance)
        {
            if (__instance.timeState != TimeState.VeryFast)
            {
                return;
            }

            int buttonIndex = veryFastButtonIndex;
            Dashboard dashboard = WorldScripts.Inst.dashboard;
            if (buttonIndex < 0 || dashboard == null || dashboard.timeToggleButtonGroup == null)
            {
                return;
            }

            List<ToggleButton> buttons = dashboard.timeToggleButtonGroup.GetToggleButtons();
            if (buttons != null && buttons.Count > buttonIndex)
            {
                dashboard.timeToggleButtonGroup.SetIsPressed(buttonIndex, true, false);
            }
        }

        private static void PlaceAfterExistingButtons(List<ToggleButton> buttons, ToggleButton template,
            ToggleButton newButton)
        {
            Transform parent = template.transform.parent;
            newButton.transform.SetAsLastSibling();

            if (parent == null || parent.GetComponent<LayoutGroup>() != null || buttons.Count < 2)
            {
                return;
            }

            RectTransform previousRect = buttons[buttons.Count - 2].transform as RectTransform;
            RectTransform lastRect = template.transform as RectTransform;
            RectTransform newRect = newButton.transform as RectTransform;
            if (previousRect == null || lastRect == null || newRect == null)
            {
                return;
            }

            Vector2 step = lastRect.anchoredPosition - previousRect.anchoredPosition;
            if (step.sqrMagnitude > 0.01f)
            {
                newRect.anchoredPosition = lastRect.anchoredPosition + step;
            }
        }
    }
}
