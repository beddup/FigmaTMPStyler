# Figma TMP Styler

One-click apply Figma text styles (outline, shadow, gradient) to Unity TextMeshPro materials — no more manually tweaking shader parameters.

---

## What problem does this solve?

In Unity UI development, designers often apply rich styling to text in Figma — **outlines, drop shadows, inner shadows, linear gradients, font size, alignment, underlines, strikethrough, letter casing**, and more. When developers implement these designs, they must manually recreate every effect in TextMeshPro's material inspector. This is tedious, error-prone, and rarely matches the design pixel-perfectly.

**Figma TMP Styler** automates this translation:

| Figma Property | TMP Mapping |
|---|---|
| Solid Color Outline | `OUTLINE_ON` shader keyword + `FaceDilate` / `OutlineWidth` |
| Drop Shadow | `UNDERLAY_ON` shader keyword + offset / softness / color |
| Inner Shadow | `UNDERLAY_INNER` shader keyword |
| Linear Gradient (text fill) | `TMP_ColorGradient` asset (horizontal / vertical) |
| Text Alignment | `TextAlignmentOptions` bitmask |
| Font Style (underline / strikethrough / case) | `FontStyles` flags |
| Font Size | `fontSize` |

**Key design insight:** Figma renders outlines as **outer strokes** (expanding outward from the glyph edge), while TMP renders them as **center strokes** (expanding equally inward and outward). This plugin compensates by synchronizing `FaceDilate` and `OutlineWidth` so the visible stroke sits on the outer edge, matching Figma's appearance.

---

## Who needs this?

- **Unity UI developers** — who work extensively with TextMeshPro and need to faithfully reproduce Figma text styles.
- **Technical artists** — who want to automate TMP material creation and eliminate manual parameter tuning.

---

## Dependencies

| Package | Description |
|---|---|
| `com.beddup.figmaclient` | Figma REST API client for fetching node data |
| `com.unity.textmeshpro` (≥ 3.0.9) | Unity TextMeshPro |

---

## Installation

Install [OpenUPM CLI](https://openupm.com/docs/getting-started.html), then run:

```bash
openupm add com.beddup.figmatmpstyler
```

or You can also download the source code directly and import it into your Unity project.

---

## How to use

### Step 1: Get your Figma Access Token

1. Log in to Figma and go to **Settings → Account → Personal Access Tokens**
2. Generate a token and copy it

### Step 2: Get the Figma text node URL

1. In Figma, select the text layer you want to style
2. Copy the url in the Web Browser

### Step 3: Configure the component

1. Select the GameObject that will display the styled text
2. Make sure it already has a `TextMeshProUGUI` component attached
3. Add a `FigmaTextTMPMaterialGenerator` component (`Add Component → Figma Text TMP Material Generator`)
4. In the Inspector, fill in:
   - **Figma Token** — paste the token from Step 1
   - **Materials Save Folder** — an `Assets`-relative path where generated `.mat` files will be saved (e.g., `Assets/Materials`)
   - **Node Link** — paste the Figma text node URL from Step 2

### Step 4: Load and apply styles

Click one of the two buttons:

| Button | Behavior                                                                                                         |
|---|------------------------------------------------------------------------------------------------------------------|
| **Load And Apply** | Fetches the text node from Figma API, reuses existing materials if available, and applies all styles in one step |
| **Load And Apply (Ignore Cache)** | Same as above, but forces regeneration of all material assets even if a cached match exists                      |

The plugin automatically:
- Fetches the text node data from the Figma API and caches it locally
- Generates TMP material files (`.mat`) in a `Materials` subfolder under your specified save path
- Sets the text content, font size, alignment, and color / gradient
- Applies outline, drop shadow, and inner shadow effects
- When **both outline/drop-shadow and inner shadow** exist on the same text, creates a duplicate child GameObject to layer the inner shadow (since a single TMP material cannot render both effects simultaneously)

---

## Material caching

The plugin uses a **deterministic naming** strategy:

- Material names are built from `font name + font size + stroke parameters + shadow parameters + color`
- Materials with identical names are reused across text nodes, avoiding duplicates
- Gradient presets (`TMP_ColorGradient`) are similarly named from `direction + start color + end color`

This means multiple text nodes that share the same styling automatically share the same material asset.

---

## Important: regenerating a TMP Font Asset

The plugin computes outline and shadow shader parameters based on values from the **TMP Font Asset** — specifically `faceInfo.pointSize`, `_ScaleRatioA`, `_ScaleRatioC`, and the font material's `gradientScale`. These values are baked into the font asset at creation time.

If you later **regenerate a TMP Font Asset** (e.g., by changing the atlas resolution, padding, or sampling point size in the Font Asset Creator), these baked values will differ from those used when the materials were originally created. As a result, previously generated outline and shadow materials will no longer visually match the Figma design.

**After regenerating any TMP Font Asset, re-apply styles to all affected text nodes using `Load And Apply (Ignore Cache)` to regenerate the materials against the updated font parameters.**

---

## Known limitations

- **Linear gradients only** (`GRADIENT_LINEAR`) — radial, angular, and diamond gradients are not supported
- **No support for multiple fills** — only the first Fill is used
- **Gradients only use the first and last stops** (`gradientStops[0]` and `[1]`), ignoring intermediate stops
- **No support for non-solid-color outlines** (e.g., gradient strokes)
- **Spread parameter** is not yet implemented
- When text has both outline/drop-shadow **and** inner shadow, a child GameObject is created to layer the effect, which may affect your RectTransform hierarchy

---

## License

MIT License. Copyright (c) 2026 Liu Wei.
