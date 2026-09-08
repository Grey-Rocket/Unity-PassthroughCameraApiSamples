using System.Collections;
using Meta.XR;
using UnityEngine;

namespace MirroredAssessment
{
    public class MirroredAssessmentManagerV6 : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private Shader m_mirrorShader;

        [SerializeField] private float m_planeSize = 16f;
        [SerializeField] private float m_imageScale = 2f;
        [SerializeField] private float m_floorOffset = 2f;
        [SerializeField] private float m_yawSensitivity = 0.011f;
        [SerializeField] private float m_secondYawSensitivity = 0.011f;
        [SerializeField] private float m_rotationSensitivity = 2.5f;
        [SerializeField] private float m_sensitivityChangeSpeed = 2f;
        [SerializeField] private float m_zoneWhereMirrorIsFull = 5;
        [SerializeField] private float m_mirrorSplit = 0.5f;
        [SerializeField] private float m_mirrorRotation = 0;


        private float m_initialYaw;
        private GameObject m_floorPlane;
        private Material m_mirrorMaterial;
        private Camera m_mainCamera;

        private Vector3 m_originalCamPosition;

        private IEnumerator Start()
        {
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
                yield return null;

            while (!m_cameraAccess.IsPlaying)
                yield return null;

            m_mainCamera = Camera.main;
            m_initialYaw = m_mainCamera.transform.eulerAngles.y;

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
            m_originalCamPosition = m_mainCamera.transform.position;
            m_floorPlane.transform.position = new Vector3(m_originalCamPosition.x, m_originalCamPosition.y - m_floorOffset, m_originalCamPosition.z);
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

            m_floorPlane.transform.position = new Vector3(m_originalCamPosition.x, m_originalCamPosition.y - m_floorOffset, m_originalCamPosition.z);

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
            if (OVRInput.GetDown(OVRInput.Button.One))
                m_initialYaw = m_mainCamera.transform.eulerAngles.y;

            float deltaYaw = Mathf.DeltaAngle(m_initialYaw, m_mainCamera.transform.eulerAngles.y);
            float splitX = Mathf.Clamp(m_mirrorSplit - Mathf.Max(0f, Mathf.Abs(deltaYaw) - m_zoneWhereMirrorIsFull) * m_yawSensitivity, 0f, m_mirrorSplit);
            m_mirrorMaterial.SetFloat("_SplitX", splitX);

            // Right joystick X axis adjusts rotation sensitivity.
            // float joystickX = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
            // m_minRotationToStartCutting = Mathf.Clamp(
            //     m_minRotationToStartCutting + joystickX * m_sensitivityChangeSpeed * Time.deltaTime,
            //     0, 90f);

            float headRoll = m_mainCamera.transform.eulerAngles.z;
            // eulerAngles.z is 0-360; remap to -180..180 so tilting left/right gives signed values.
            if (headRoll > 180f) headRoll -= 360f;
            float mirrorRotation = headRoll * m_rotationSensitivity + deltaYaw * m_secondYawSensitivity;
            m_mirrorMaterial.SetFloat("_MirrorRotation", mirrorRotation);
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
