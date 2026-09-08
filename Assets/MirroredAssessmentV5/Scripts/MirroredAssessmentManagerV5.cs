using System.Collections;
using Meta.XR;
using UnityEngine;

namespace MirroredAssessment
{
    public class MirroredAssessmentManagerV5 : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private Shader m_mirrorShader;

        // > 1 zooms out (shows more of the camera image); < 1 zooms in.
        [SerializeField] private float m_imageScale = 2.0f;
        [SerializeField] private float m_verticalOffset = 0f;
        // When false: left half is the source, right half is its mirror.
        // When true:  right half is the source, left half is its mirror.
        [SerializeField] private bool m_mirrorRightSide = false;
        // Positive = gap between the two mirrored halves; negative = they overlap.
        [SerializeField] private float m_separation = 0f;
        // Side length of the centre square as a fraction of screen height (0–1).
        [SerializeField] private float m_squareSize = 0.8f;

        private GameObject m_screenQuad;
        private Material m_mirrorMaterial;
        private Camera m_mainCamera;

        private IEnumerator Start()
        {
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
                yield return null;

            while (!m_cameraAccess.IsPlaying)
                yield return null;

            m_mainCamera = Camera.main;

            m_mirrorMaterial = new Material(m_mirrorShader);
            m_mirrorMaterial.SetTexture("_MainTex", m_cameraAccess.GetTexture());
            m_mirrorMaterial.SetFloat("_UVScale", m_imageScale);
            m_mirrorMaterial.SetFloat("_MirrorRight", m_mirrorRightSide ? 1f : 0f);
            m_mirrorMaterial.SetFloat("_Separation", m_separation);
            SetSquareUniforms();

            yield return null;

            CreateScreenQuad();
        }

        private void CreateScreenQuad()
        {
            m_screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(m_screenQuad.GetComponent<Collider>());
            m_screenQuad.name = "MirrorScreenQuadV5";

            m_screenQuad.transform.SetParent(m_mainCamera.transform, false);
            m_screenQuad.transform.localPosition = new Vector3(0f, m_verticalOffset, 100f);

            // Oversize the quad (3x FOV coverage) so the viewport is always fully covered.
            float halfHeight = Mathf.Tan(m_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 100f;
            float halfWidth  = halfHeight * m_mainCamera.aspect;
            m_screenQuad.transform.localScale = new Vector3(halfWidth * 2f * 3f, halfHeight * 2f * 3f, 1f);

            var mr = m_screenQuad.GetComponent<MeshRenderer>();
            mr.material = m_mirrorMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Update()
        {
            if (m_screenQuad == null) return;

            m_mirrorMaterial.SetFloat("_UVScale", m_imageScale);
            m_mirrorMaterial.SetFloat("_MirrorRight", m_mirrorRightSide ? 1f : 0f);
            m_mirrorMaterial.SetFloat("_Separation", m_separation);
            SetSquareUniforms();
        }

        // The quad is 3x the screen size, so the visible screen occupies UV [1/3, 2/3].
        // halfY = squareSize * 0.5 * (1/3); halfX accounts for aspect ratio.
        private void SetSquareUniforms()
        {
            float halfY = m_squareSize * 0.5f / 3.0f;
            float halfX = halfY / m_mainCamera.aspect;
            m_mirrorMaterial.SetFloat("_SquareHalfX", halfX);
            m_mirrorMaterial.SetFloat("_SquareHalfY", halfY);
        }

        private void OnDestroy()
        {
            if (m_screenQuad != null)
                Destroy(m_screenQuad);

            if (m_mirrorMaterial != null)
                Destroy(m_mirrorMaterial);
        }
    }
}
