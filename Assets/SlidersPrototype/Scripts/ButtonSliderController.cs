using PassthroughCameraSamples.StartScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlidersPrototype
{
    /// <summary>
    /// Spawns a world-space panel with a slider controlled by pointing the
    /// controller ray at the − / + buttons and pressing the index trigger.
    ///
    /// Requirements in the scene:
    ///   • OVRCameraRig
    ///   • UIHelpers prefab  (Assets/…/StartScene/Prefabs/UIHelpers.prefab)
    ///     which contains the EventSystem + OVRInputModule + LaserPointer.
    /// </summary>
    public class ButtonSliderController : MonoBehaviour
    {
        [Header("Slider settings")]
        [SerializeField] private string m_sliderLabel = "Value";
        [SerializeField] private float m_minValue = 0f;
        [SerializeField] private float m_maxValue = 100f;
        [SerializeField] private float m_initialValue = 50f;
        [SerializeField] private float m_stepSize = 1f;

        [Header("Panel placement")]
        [SerializeField] [Range(0.3f, 3f)] private float m_panelDistance = 1.0f;
        [SerializeField] [Range(-1f, 1f)]  private float m_panelHeightOffset = -0.1f;

        /// <summary>Current slider value — read by other scripts.</summary>
        public float Value;

        private Slider m_slider;
        private Text   m_valueLabel;

        private void Start()
        {
            Value = Mathf.Clamp(m_initialValue, m_minValue, m_maxValue);
            BuildUI();
        }

        // ── UI construction ───────────────────────────────────────────────

        private void BuildUI()
        {
            // ── Canvas ────────────────────────────────────────────────────
            var canvasGO = new GameObject("SliderPanel");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            // OVRInputModule only hits OVRRaycaster components, not plain GraphicRaycaster.
            var raycaster = canvasGO.AddComponent<OVRRaycaster>();

            // Wire the laser pointer so OVRRaycaster knows which ray to use.
            var laserPointer = FindFirstObjectByType<LaserPointer>();
            if (laserPointer != null)
                raycaster.pointer = laserPointer.gameObject;
            else
                Debug.LogWarning("[ButtonSliderController] No LaserPointer found. " +
                                 "Add the UIHelpers prefab to your scene.");

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta   = new Vector2(600, 200);
            rt.localScale  = Vector3.one * 0.001f; // 1 px → 1 mm

            PlacePanel(canvasGO);

            // ── Background ────────────────────────────────────────────────
            AddImage(MakeRect("BG", canvasGO.transform, Vector2.zero, Vector2.one),
                     new Color(0f, 0f, 0f, 0.75f));

            // ── Value label (top) ─────────────────────────────────────────
            var labelRT  = MakeRect("Label", canvasGO.transform,
                                    new Vector2(0f, 0.65f), Vector2.one);
            m_valueLabel = AddText(labelRT, LabelText(), 32, TextAnchor.MiddleCenter);

            // ── Control row (bottom two-thirds) ───────────────────────────
            var row = MakeRect("Row", canvasGO.transform,
                               new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.65f));

            // − button
            var decRT = MakeRect("DecBtn", row, Vector2.zero, new Vector2(0.15f, 1f));
            MakeButton(decRT, "−", () => Step(-m_stepSize));

            // + button
            var incRT = MakeRect("IncBtn", row, new Vector2(0.85f, 0f), Vector2.one);
            MakeButton(incRT, "+", () => Step(+m_stepSize));

            // slider (middle)
            BuildSlider(row);

            // ── Hint label (bottom) ───────────────────────────────────────
            var hintRT = MakeRect("Hint", canvasGO.transform,
                                  Vector2.zero, new Vector2(1f, 0.12f));
            AddText(hintRT, "Point & pull trigger  ·  drag slider to set exact value",
                    18, TextAnchor.MiddleCenter).color = new Color(0.6f, 0.6f, 0.6f);
        }

        private void BuildSlider(RectTransform parent)
        {
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(parent, false);
            m_slider = sliderGO.AddComponent<Slider>();

            var sliderRT      = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.17f, 0.1f);
            sliderRT.anchorMax = new Vector2(0.83f, 0.9f);
            sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

            // track background
            var track    = MakeRect("Track", sliderGO.transform,
                                    new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
            var trackImg = AddImage(track, new Color(0.3f, 0.3f, 0.3f));
            m_slider.targetGraphic = trackImg;

            // fill area
            var fillArea = MakeRect("FillArea", sliderGO.transform,
                                    new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
            fillArea.offsetMin = new Vector2(4, 0);
            fillArea.offsetMax = new Vector2(-4, 0);

            var fill    = MakeRect("Fill", fillArea, Vector2.zero, new Vector2(0f, 1f));
            AddImage(fill, new Color(0.2f, 0.6f, 1f));
            m_slider.fillRect = fill;

            // handle area & handle
            var handleArea = MakeRect("HandleArea", sliderGO.transform,
                                       Vector2.zero, Vector2.one);
            var handle     = MakeRect("Handle", handleArea,
                                       new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            handle.sizeDelta = new Vector2(36, 0);
            AddImage(handle, Color.white);
            m_slider.handleRect = handle;

            m_slider.minValue = m_minValue;
            m_slider.maxValue = m_maxValue;
            m_slider.value    = Value;
            m_slider.onValueChanged.AddListener(OnSliderMoved);
        }

        // ── value change helpers ──────────────────────────────────────────

        private void Step(float delta)
        {
            Value = Mathf.Clamp(Value + delta, m_minValue, m_maxValue);
            SyncSlider();
        }

        private void OnSliderMoved(float v)
        {
            Value = v;
            UpdateLabel();
        }

        private void SyncSlider()
        {
            if (m_slider == null) return;
            m_slider.onValueChanged.RemoveListener(OnSliderMoved);
            m_slider.value = Value;
            m_slider.onValueChanged.AddListener(OnSliderMoved);
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (m_valueLabel != null)
                m_valueLabel.text = LabelText();
        }

        private string LabelText() => $"{m_sliderLabel}: {Value:F1}";

        // ── placement ─────────────────────────────────────────────────────

        private void PlacePanel(GameObject panel)
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            panel.transform.position = cam.transform.position
                + fwd * m_panelDistance
                + Vector3.up * m_panelHeightOffset;
            panel.transform.rotation = Quaternion.LookRotation(fwd);
        }

        // ── UI helpers ────────────────────────────────────────────────────

        private static void MakeButton(RectTransform rt, string label,
                                        System.Action onStep)
        {
            var bg  = AddImage(rt, new Color(0.18f, 0.18f, 0.18f));
            var btn = rt.gameObject.AddComponent<Button>();

            var colors              = btn.colors;
            colors.normalColor      = new Color(0.18f, 0.18f, 0.18f);
            colors.highlightedColor = new Color(0.30f, 0.50f, 0.90f);
            colors.pressedColor     = new Color(0.10f, 0.30f, 0.70f);
            colors.selectedColor    = new Color(0.18f, 0.18f, 0.18f);
            btn.colors              = colors;
            btn.targetGraphic       = bg;

            // HoldButton fires onStep on first press and then repeatedly while held.
            var hold    = rt.gameObject.AddComponent<HoldButton>();
            hold.onStep = onStep;

            var txtRT = MakeRect("Text", rt, Vector2.zero, Vector2.one);
            AddText(txtRT, label, 46, TextAnchor.MiddleCenter);
        }

        private static RectTransform MakeRect(string name, Transform parent,
                                               Vector2 anchorMin, Vector2 anchorMax)
        {
            var go          = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r           = go.AddComponent<RectTransform>();
            r.anchorMin     = anchorMin;
            r.anchorMax     = anchorMax;
            r.offsetMin     = r.offsetMax = Vector2.zero;
            return r;
        }

        private static Image AddImage(RectTransform rt, Color color)
        {
            var img   = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text AddText(RectTransform rt, string content,
                                     int fontSize, TextAnchor alignment)
        {
            var txt       = rt.gameObject.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = fontSize;
            txt.alignment = alignment;
            txt.color     = Color.white;
            txt.text      = content;
            return txt;
        }
    }

    /// <summary>
    /// Fires onStep immediately on pointer-down, then repeatedly while the
    /// trigger is held (after holdDelay seconds, at repeatRate steps/second).
    /// OVRInputModule raises IPointerDown/Up events when the index trigger is
    /// pressed/released while the ray hovers over this object.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action onStep;
        public float holdDelay  = 0.4f;
        public float repeatRate = 8f;

        private bool  m_held;
        private float m_heldTime;
        private float m_repeatAccum;

        public void OnPointerDown(PointerEventData _)
        {
            m_held        = true;
            m_heldTime    = 0f;
            m_repeatAccum = 0f;
            onStep?.Invoke();   // fire immediately on first press
        }

        public void OnPointerUp(PointerEventData _) => m_held = false;

        private void Update()
        {
            if (!m_held) return;
            m_heldTime += Time.deltaTime;
            if (m_heldTime < holdDelay) return;

            m_repeatAccum += Time.deltaTime * repeatRate;
            while (m_repeatAccum >= 1f)
            {
                onStep?.Invoke();
                m_repeatAccum -= 1f;
            }
        }
    }
}
