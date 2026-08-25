using System.Collections;
using Meta.XR;
using UnityEngine;

namespace MirroredAssessment
{
    public class MirroredAssessmentManagerV3 : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private Shader m_mirrorShader;

        [Tooltip("Horizontal size of the mirror plane in metres.")]
        [SerializeField] [Range(1f, 20f)] private float m_planeSize = 6f;

        [Tooltip("UV zoom factor. > 1 zooms out (less magnified). Increase if the image looks too zoomed in.")]
        [SerializeField] [Range(0.5f, 4f)] private float m_imageScale = 1.5f;

        [Tooltip("How much UV shifts per degree of head yaw. Flip the sign if the fold moves the wrong way.")]
        [SerializeField] [Range(0f, 0.05f)] private float m_yawSensitivity = 0.011f;

        [Tooltip("How far below the camera centre the mirror plane sits (metres). Tune to match your floor height.")]
        [SerializeField] [Range(0.5f, 2.5f)] private float m_floorOffset = 1.4f;

        private GameObject m_floorPlane;
        private Material m_mirrorMaterial;
        private Camera m_mainCamera;
        private float m_initialYaw;

        private IEnumerator Start()
        {
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
                yield return null;

            while (!m_cameraAccess.IsPlaying)
                yield return null;

            m_mainCamera = Camera.main;
            m_initialYaw = m_mainCamera.transform.eulerAngles.y;

            // Keep the passthrough layer visible — we want normal passthrough when looking forward.
            // The floor plane only covers the view when the user looks down.

            m_mirrorMaterial = new Material(m_mirrorShader);
            m_mirrorMaterial.SetTexture("_MainTex", m_cameraAccess.GetTexture());
            m_mirrorMaterial.SetFloat("_UVScale", m_imageScale);

            yield return null;

            CreateFloorMirrorPlane();
        }

        private void CreateFloorMirrorPlane()
        {
            m_floorPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(m_floorPlane.GetComponent<Collider>());
            m_floorPlane.name = "MirrorFloorPlane";

            // Fixed world position directly below the user at startup — does not follow them.
            Vector3 camPos = m_mainCamera.transform.position;
            m_floorPlane.transform.position = new Vector3(camPos.x, camPos.y - m_floorOffset, camPos.z);
            // Euler(90, 0, 0) rotates the quad's normal from +Z to +Y — horizontal floor.
            m_floorPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            m_floorPlane.transform.localScale = new Vector3(m_planeSize, m_planeSize, 1f);

            var mr = m_floorPlane.GetComponent<MeshRenderer>();
            mr.material = m_mirrorMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Update()
        {
            if (m_floorPlane == null) return;
            m_mirrorMaterial.SetFloat("_UVScale", m_imageScale);

            // Pass a mono (center-eye) VP matrix so both stereo eyes sample the
            // same UV — prevents double-vision caused by stereo parallax on a
            // close floor plane.
            Matrix4x4 vp = m_mainCamera.projectionMatrix * m_mainCamera.worldToCameraMatrix;
            m_mirrorMaterial.SetMatrix("_MonoCameraVP", vp);

            // Map head yaw to mirror split:
            //   center (deltaYaw = 0) → splitX = 0.5 (left half mirrored, right half normal)
            //   looking right (positive deltaYaw) → splitX → 0 (no mirror)
            //   looking left (negative deltaYaw) → clamped at 0.5 (still 50/50)
            float deltaYaw = Mathf.DeltaAngle(m_initialYaw, m_mainCamera.transform.eulerAngles.y);
            float splitX = Mathf.Clamp(0.5f - deltaYaw * m_yawSensitivity, 0f, 0.5f);
            m_mirrorMaterial.SetFloat("_SplitX", splitX);
        }

        private void OnDestroy()
        {
            if (m_floorPlane != null)
                Destroy(m_floorPlane);

            if (m_mirrorMaterial != null)
                Destroy(m_mirrorMaterial);
        }
    }
}
