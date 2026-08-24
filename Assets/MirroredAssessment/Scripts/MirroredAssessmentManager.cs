using System.Collections;
using Meta.XR;
using UnityEngine;

namespace MirroredAssessment
{
    public class MirroredAssessmentManager : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private Shader m_mirrorShader;
        [Tooltip("Scale the image down until proportions match real life. 1 = fill display FOV.")]
        [SerializeField] [Range(0.1f, 1f)] private float m_imageScale = 0.7f;

        private GameObject m_fullScreenQuad;
        private Material m_mirrorMaterial;
        private OVRPassthroughLayer m_passthroughLayer;
        private Camera m_mainCamera;

        private IEnumerator Start()
        {
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
                yield return null;

            while (!m_cameraAccess.IsPlaying)
                yield return null;

            m_mainCamera = Camera.main;

            m_passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
            if (m_passthroughLayer != null)
                m_passthroughLayer.hidden = true;

            m_mirrorMaterial = new Material(m_mirrorShader);
            m_mirrorMaterial.SetTexture("_MainTex", m_cameraAccess.GetTexture());
            m_mirrorMaterial.renderQueue = 5000;

            // Wait one frame so OVR has set the real FOV/aspect before we size the quad
            yield return null;

            CreateFullScreenQuad();
        }

        private void CreateFullScreenQuad()
        {
            m_fullScreenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(m_fullScreenQuad.GetComponent<Collider>());
            m_fullScreenQuad.name = "MirrorFullScreen";
            m_fullScreenQuad.transform.SetParent(m_mainCamera.transform, false);

            // Place far away so stereo parallax between eyes is imperceptible.
            // ZTest Always in the shader keeps it on top regardless of depth.
            float d = 100f;
            m_fullScreenQuad.transform.localPosition = new Vector3(0f, 0f, d);
            m_fullScreenQuad.transform.localRotation = Quaternion.identity;

            float h = 2f * d * Mathf.Tan(m_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * m_imageScale;
            float w = h * m_mainCamera.aspect;
            m_fullScreenQuad.transform.localScale = new Vector3(w, h, 1f);

            var mr = m_fullScreenQuad.GetComponent<MeshRenderer>();
            mr.material = m_mirrorMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (m_fullScreenQuad != null)
                Destroy(m_fullScreenQuad);

            if (m_mirrorMaterial != null)
                Destroy(m_mirrorMaterial);

            if (m_passthroughLayer != null)
                m_passthroughLayer.hidden = false;
        }
    }
}
