using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace InfraSpaceQoL
{
    internal static class SpeedPatches
    {
        private sealed class SpeedOption
        {
            internal readonly string Name;
            internal readonly string Title;
            internal readonly string Resource;
            internal readonly TimeState State;
            internal readonly int Multiplier;
            internal int ButtonIndex = -1;
            internal Sprite[] Sprites;

            internal SpeedOption(string name, string title, string resource, TimeState state, int multiplier)
            {
                Name = name;
                Title = title;
                Resource = resource;
                State = state;
                Multiplier = multiplier;
            }
        }

        private static readonly SpeedOption[] Options =
        {
            new SpeedOption("InfraSpaceQoL_VeryFastButton", "Very Fast", "InfraSpaceQoL.Speed4x.png", TimeState.VeryFast, 4),
            new SpeedOption("InfraSpaceQoL_SuperspeedButton", "Superspeed", "InfraSpaceQoL.Speed8x.png", TimeState.Superspeed, 8)
        };

        internal static void DashboardStartPostfix(Dashboard __instance)
        {
            AddSpeedButtons(__instance);
        }

        internal static void DashboardStartAfterLoadPostfix(Dashboard __instance)
        {
            AddSpeedButtons(__instance);
        }

        private static void AddSpeedButtons(Dashboard dashboard)
        {
            ToggleButtonGroup group = dashboard.timeToggleButtonGroup;
            List<ToggleButton> buttons = group == null ? null : group.GetToggleButtons();
            if (buttons == null || buttons.Count == 0)
            {
                Debug.LogWarning("[InfraSpaceQoL] Speed controls are not ready; waiting for the dashboard after load.");
                return;
            }

            // Clone the native Fast control rather than a previously added custom button.
            ToggleButton template = buttons[buttons.Count - 1];
            foreach (ToggleButton button in buttons)
            {
                if (button != null && !button.gameObject.name.StartsWith("InfraSpaceQoL_", StringComparison.Ordinal))
                {
                    template = button;
                }
            }

            foreach (SpeedOption option in Options)
            {
                option.ButtonIndex = -1;
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i] != null && buttons[i].gameObject.name == option.Name)
                    {
                        option.ButtonIndex = i;
                        break;
                    }
                }
                if (option.ButtonIndex >= 0)
                {
                    continue;
                }

                GameObject clone = null;
                ToggleButton added = null;
                try
                {
                    clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
                    clone.name = option.Name;
                    added = clone.GetComponent<ToggleButton>();
                    if (added == null)
                    {
                        throw new InvalidOperationException("The cloned speed control has no ToggleButton.");
                    }

                    PlaceAfterExistingButtons(buttons, added);
                    ConfigureArtwork(option, added);
                    if (added.text != null)
                    {
                        added.text.text = option.Multiplier + "x";
                    }
                    foreach (TooltipHoverable tooltip in clone.GetComponentsInChildren<TooltipHoverable>(true))
                    {
                        // Otherwise Start() restores the inherited Speed x2 localization.
                        tooltip.titleCode = string.Empty;
                        tooltip.descriptionCode = string.Empty;
                        tooltip.Init(option.Title, "Game speed: " + option.Multiplier + "x");
                    }

                    option.ButtonIndex = buttons.Count;
                    buttons.Add(added);
                    added.SetToggleButtonGroup(group);
                    added.SetIsPressed(false, false);
                    clone.SetActive(true);
                    Debug.Log("[InfraSpaceQoL] Added " + option.Multiplier + "x speed with its own icon at index " + option.ButtonIndex + ".");
                }
                catch (Exception exception)
                {
                    if (added != null)
                    {
                        buttons.Remove(added);
                    }
                    option.ButtonIndex = -1;
                    if (clone != null)
                    {
                        UnityEngine.Object.Destroy(clone);
                    }
                    Debug.LogError("[InfraSpaceQoL] Could not add the " + option.Multiplier + "x speed button. " + exception);
                }
            }
        }

        internal static TimeState? GetTimeStateForButton(int buttonIndex)
        {
            foreach (SpeedOption option in Options)
            {
                if (option.ButtonIndex >= 0 && option.ButtonIndex == buttonIndex)
                {
                    return option.State;
                }
            }
            return null;
        }

        internal static int GetButtonIndexForState(TimeState state)
        {
            foreach (SpeedOption option in Options)
            {
                if (option.State == state)
                {
                    return option.ButtonIndex;
                }
            }
            return -1;
        }

        internal static bool TimeButtonSelectionPrefix(TimeManager __instance)
        {
            Dashboard dashboard = WorldScripts.Inst.dashboard;
            if (dashboard == null || dashboard.timeToggleButtonGroup == null)
            {
                return true;
            }
            TimeState? state = GetTimeStateForButton(dashboard.timeToggleButtonGroup.selectedButton);
            if (!state.HasValue)
            {
                return true;
            }
            __instance.SetTimeState(state.Value, true, true);
            return false;
        }

        internal static void TimeStatePostfix(TimeManager __instance)
        {
            int index = GetButtonIndexForState(__instance.timeState);
            Dashboard dashboard = WorldScripts.Inst.dashboard;
            if (index < 0 || dashboard == null || dashboard.timeToggleButtonGroup == null)
            {
                return;
            }
            List<ToggleButton> buttons = dashboard.timeToggleButtonGroup.GetToggleButtons();
            if (buttons != null && index < buttons.Count)
            {
                dashboard.timeToggleButtonGroup.SetIsPressed(index, true, false);
            }
        }

        private static void ConfigureArtwork(SpeedOption option, ToggleButton button)
        {
            if (option.Sprites == null)
            {
                option.Sprites = LoadSprites(option.Resource);
            }
            button.buttonDefault = option.Sprites[0];
            button.buttonHighlighted = option.Sprites[1];
            button.buttonDown = option.Sprites[2];
            button.buttonUnpressable = option.Sprites[3];
            Image image = button.GetComponent<Image>();
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.sprite = option.Sprites[0];
        }

        private static Sprite[] LoadSprites(string resource)
        {
            byte[] data;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    throw new InvalidDataException("Missing embedded speed icon: " + resource);
                }
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    data = buffer.ToArray();
                }
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(source, data))
                {
                    throw new InvalidDataException("Could not decode speed icon: " + resource);
                }
                // Artwork is a black-and-white mask; luminance supplies the sprite alpha.
                Color32[] mask = source.GetPixels32();
                Color32[] tints =
                {
                    new Color32(112, 201, 248, 255), new Color32(235, 247, 255, 255),
                    new Color32(61, 110, 149, 255), new Color32(45, 76, 100, 255)
                };
                var sprites = new Sprite[tints.Length];
                for (int i = 0; i < tints.Length; i++)
                {
                    Color32[] pixels = BuildIconPixels(mask, source.width, source.height, tints[i]);
                    var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    texture.name = resource + "-" + i;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    // Keep the transparent button padding. A tight sprite rect stretches the
                    // painted arrows across the entire hit target, making them bar-height.
                    sprites[i] = Sprite.Create(texture, new Rect(0, 0, source.width, source.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                }
                return sprites;
            }
            finally
            {
                UnityEngine.Object.Destroy(source);
            }
        }

        internal static Color32[] BuildIconPixels(Color32[] mask, int width, int height, Color32 tint)
        {
            var alpha = new byte[mask.Length];
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int p = y * width + x;
                    byte value = (byte)Math.Max(mask[p].r, Math.Max(mask[p].g, mask[p].b));
                    alpha[p] = value < 16 ? (byte)0 : value > 239 ? (byte)255 : value;
                    if (alpha[p] > 8)
                    {
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    }
                }
            }
            if (maxX < minX)
            {
                throw new InvalidDataException("Speed icon is empty.");
            }

            // Native controls paint their glyph inside the larger clickable button rect.
            // Preserve that inset: equal glyph height, centered, with room between buttons.
            int contentWidth = maxX - minX + 1, contentHeight = maxY - minY + 1;
            float scale = Math.Min(width * 0.8f / contentWidth, height * 0.45f / contentHeight);
            int drawWidth = Math.Max(1, (int)Math.Round(contentWidth * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(contentHeight * scale));
            int offsetX = (width - drawWidth) / 2, offsetY = (height - drawHeight) / 2;
            var pixels = new Color32[mask.Length];
            for (int y = 0; y < drawHeight; y++)
            {
                for (int x = 0; x < drawWidth; x++)
                {
                    float sx = minX + (x + 0.5f) * contentWidth / drawWidth - 0.5f;
                    float sy = minY + (y + 0.5f) * contentHeight / drawHeight - 0.5f;
                    int x0 = Math.Max(0, Math.Min(width - 1, (int)Math.Floor(sx)));
                    int y0 = Math.Max(0, Math.Min(height - 1, (int)Math.Floor(sy)));
                    int x1 = Math.Min(width - 1, x0 + 1), y1 = Math.Min(height - 1, y0 + 1);
                    float tx = sx - (float)Math.Floor(sx), ty = sy - (float)Math.Floor(sy);
                    float lower = alpha[y0 * width + x0] * (1f - tx) + alpha[y0 * width + x1] * tx;
                    float upper = alpha[y1 * width + x0] * (1f - tx) + alpha[y1 * width + x1] * tx;
                    byte value = (byte)Math.Round((lower * (1f - ty) + upper * ty) * tint.a / 255f);
                    pixels[(offsetY + y) * width + offsetX + x] = new Color32(tint.r, tint.g, tint.b, value);
                }
            }
            return pixels;
        }

        private static void PlaceAfterExistingButtons(List<ToggleButton> buttons, ToggleButton newButton)
        {
            Transform parent = newButton.transform.parent;
            newButton.transform.SetAsLastSibling();
            if (parent == null || parent.GetComponent<LayoutGroup>() != null || buttons.Count < 2)
            {
                return;
            }
            RectTransform previous = buttons[buttons.Count - 2].transform as RectTransform;
            RectTransform last = buttons[buttons.Count - 1].transform as RectTransform;
            RectTransform next = newButton.transform as RectTransform;
            if (previous != null && last != null && next != null)
            {
                Vector2 step = last.anchoredPosition - previous.anchoredPosition;
                if (step.sqrMagnitude > 0.01f)
                {
                    next.anchoredPosition = last.anchoredPosition + step;
                }
            }
        }
    }
}
