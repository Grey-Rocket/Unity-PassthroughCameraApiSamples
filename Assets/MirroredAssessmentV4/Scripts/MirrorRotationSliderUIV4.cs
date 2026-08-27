using UnityEngine;
using UnityEngine.UI;

namespace MirroredAssessment
{
    /// <summary>
    /// Spawns a world-space slider panel in front of the user that controls
    /// the rotation sensitivity on MirroredAssessmentManagerV4.
    /// </summary>
    public class MirrorRotationSliderUIV4 : MonoBehaviour
    {
                [SerializeField] private MirroredAssessmentManagerV3 m_manager;

        [Tooltip("Distance in front of the camera where the panel appears.")]
        [SerializeField] [Range(0.3f, 2f)] private float m_panelDistance = 0.6f;

        [Tooltip("Height offset from camera centre (negative = below eye level).")]
        [SerializeField] [Range(-1f, 1f)] private float m_panelHeightOffset = -0.2f;

        private Slider m_slider;
        private Text m_label;

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("RotationSensitivityPanel");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<GraphicRaycaster>();

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 120);
            rt.localScale = Vector3.one * 0.001f; // 1 unit = 1 mm at this scale

            PositionPanel(canvasGO);

            // Background panel
            var bg = MakeRect("Background", canvasGO.transform);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(canvasGO.transform, false);
            m_label = labelGO.AddComponent<Text>();
            m_label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_label.fontSize = 28;
            m_label.alignment = TextAnchor.MiddleCenter;
            m_label.color = Color.white;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0.55f);
            labelRT.anchorMax = new Vector2(1, 1f);
            labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

            // Slider
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(canvasGO.transform, false);
            m_slider = sliderGO.AddComponent<Slider>();

            var sliderRT = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.05f, 0.05f);
            sliderRT.anchorMax = new Vector2(0.95f, 0.5f);
            sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

            // Slider background
            var bgTrack = MakeRect("Background", sliderGO.transform);
            bgTrack.anchorMin = new Vector2(0, 0.25f);
            bgTrack.anchorMax = new Vector2(1, 0.75f);
            bgTrack.offsetMin = bgTrack.offsetMax = Vector2.zero;
            var bgTrackImg = bgTrack.gameObject.AddComponent<Image>();
            bgTrackImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            m_slider.targetGraphic = bgTrackImg;

            // Fill area
            var fillArea = MakeRect("Fill Area", sliderGO.transform);
            fillArea.anchorMin = new Vector2(0, 0.25f);
            fillArea.anchorMax = new Vector2(1, 0.75f);
            fillArea.offsetMin = new Vector2(5, 0);
            fillArea.offsetMax = new Vector2(-5, 0);

            var fill = MakeRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            m_slider.fillRect = fill;

            // Handle area
            var handleArea = MakeRect("Handle Mirror Rotation Size", sliderGO.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;

            var handle = MakeRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(30, 0);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            m_slider.handleRect = handle;

            // Slider range matches the inspector range on m_rotationSensitivity
            m_slider.minValue = 0f;
            m_slider.maxValue = 90f;
            m_slider.value = m_manager.MirrorShrinkingSizeHandler;
            m_slider.onValueChanged.AddListener(OnSliderChanged);

            UpdateLabel(m_slider.value);
        }

        private void PositionPanel(GameObject panel)
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 forward = cam.transform.forward;
            forward.y = 0;
            forward.Normalize();

            panel.transform.position = cam.transform.position
                + forward * m_panelDistance
                + Vector3.up * m_panelHeightOffset;
            panel.transform.rotation = Quaternion.LookRotation(forward);
        }

        private void Update()
        {
            // Keep the slider in sync when the joystick changes the value externally.
            if (m_slider != null && !Mathf.Approximately(m_slider.value, m_manager.MirrorShrinkingSizeHandler))
            {
                m_slider.onValueChanged.RemoveListener(OnSliderChanged);
                m_slider.value = m_manager.MirrorShrinkingSizeHandler;
                m_slider.onValueChanged.AddListener(OnSliderChanged);
                UpdateLabel(m_slider.value);
            }
        }

        private void OnSliderChanged(float value)
        {
            m_manager.MirrorShrinkingSizeHandler = value;
            UpdateLabel(value);
        }

        private void UpdateLabel(float value)
        {
            if (m_label != null)
                m_label.text = $"Yaw Dead Zone: {value:F2}";
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }
    }
}
