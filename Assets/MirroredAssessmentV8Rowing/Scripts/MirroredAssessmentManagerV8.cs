using System.Collections;
using Meta.XR;
using UnityEngine;

namespace MirroredAssessment
{
    public class MirroredAssessmentManagerV8 : MonoBehaviour
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

        // Renderers on this layer appear only inside the mirror, folded along with
        // the camera image. They are hidden from the eye camera, so the mirror is
        // the only route they have to the display.
        [SerializeField] private LayerMask m_mirrorContentLayer = 1 << 8;
        // Content render-texture size as a multiple of the passthrough resolution.
        [SerializeField] private int m_contentTextureScale = 2;

        // Hierarchies to pull into the mirror, children included — the hand-tracking
        // blocks, typically. Separate from simply setting the layer in the inspector
        // because OVRSkeleton and OVRSkeletonRenderer spawn bone objects at runtime,
        // and a new GameObject starts on layer 0 rather than inheriting its parent's.
        [SerializeField] private Transform[] m_mirrorContentRoots;
        // Also pull in every Interaction SDK HandVisual, found at runtime. These are
        // the hands this scene actually draws: the hand-tracking building blocks sit
        // under LeftHandAnchor/RightHandAnchor, which are deactivated, so they never run.
        [SerializeField] private bool m_includeHandVisuals = true;
        // Colour for mirror content whose material has no _BaseColor (the hands).
        // Published as the global _BaseColor; see MirrorContentShaderV8.
        [SerializeField] private Color m_handColor = new Color(1f, 0.85f, 0.72f, 1f);

        // Optional object to move within reach when the mirror starts, so it does not
        // depend on where the tracking origin happens to put it. The offset is in head
        // space with pitch and roll ignored: x right, y up, z forward, in metres. The
        // small positive x keeps it in the right half of the camera image, the half
        // the mirror samples when m_mirrorRightSide is on.
        [SerializeField] private Transform m_reachTarget;
        [SerializeField] private Vector3 m_reachOffset = new Vector3(0.1f, -0.15f, 0.4f);
        // Replacement shader for the content camera: everything in the mirror draws
        // with this instead of its own material. Required because the stock hand
        // material never writes the alpha the composite masks with, and because
        // OVRMeshRenderer re-assigns sharedMaterial every frame, so a material swap
        // does not survive. Leave unset to draw materials as-is.
        [SerializeField] private Shader m_mirrorContentShader;

        // Logs the mirror-content pipeline's state to logcat (tag "[MirrorV8]") every
        // m_diagnosticsInterval seconds. For on-device debugging; turn off when done.
        [SerializeField] private bool m_logDiagnostics = true;
        [SerializeField] private float m_diagnosticsInterval = 2f;
        private float m_nextDiagnosticsTime;

        private GameObject m_screenQuad;
        private Material m_mirrorMaterial;
        private Camera m_mainCamera;
        private Camera m_contentCamera;
        private RenderTexture m_contentTexture;
        // m_mirrorContentRoots plus any discovered HandVisuals.
        private readonly System.Collections.Generic.List<Transform> m_contentRoots =
            new System.Collections.Generic.List<Transform>();

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

            CreateContentCamera();

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

        // A second camera renders the mirror-content layer from the passthrough
        // camera's point of view into a transparent render texture. The mirror shader
        // samples that texture with the same UVs it uses for the camera image, so
        // virtual content sits in the right place and gets mirrored along with it.
        private void CreateContentCamera()
        {
            Vector2Int res = m_cameraAccess.CurrentResolution * Mathf.Max(1, m_contentTextureScale);

            m_contentTexture = new RenderTexture(res.x, res.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "MirrorContentV8",
                // The composite minifies this texture heavily at high m_imageScale;
                // mips keep virtual content from shimmering.
                useMipMap = true,
                autoGenerateMips = true
            };

            var go = new GameObject("MirrorContentCameraV8");
            m_contentCamera = go.AddComponent<Camera>();
            m_contentCamera.targetTexture = m_contentTexture;
            m_contentCamera.cullingMask = m_mirrorContentLayer;
            m_contentCamera.clearFlags = CameraClearFlags.SolidColor;
            // Alpha is the mask the mirror shader blends with, so clear it to zero.
            m_contentCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_contentCamera.stereoTargetEye = StereoTargetEyeMask.None;
            m_contentCamera.nearClipPlane = 0.01f;
            // Driven by hand from Update so its pose always matches the frame's image.
            m_contentCamera.enabled = false;

            // Keep the content out of the eye buffer. Without this it would also be
            // drawn straight to the display, where the quad paints over it anyway.
            m_mainCamera.cullingMask &= ~(int)m_mirrorContentLayer;

            m_mirrorMaterial.SetTexture("_VirtualTex", m_contentTexture);
            m_mirrorMaterial.SetFloat("_VirtualTexOn", 1f);

            // An empty tag means every object draws with the shader's first SubShader,
            // no matter what RenderType its own material declares.
            if (m_mirrorContentShader != null)
                m_contentCamera.SetReplacementShader(m_mirrorContentShader, "");
            Shader.SetGlobalColor("_BaseColor", m_handColor);

            CollectContentRoots();
            PlaceReachTarget();
        }

        private void PlaceReachTarget()
        {
            if (m_reachTarget == null) return;

            Transform head = m_mainCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward; // looking straight up or down
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            m_reachTarget.SetPositionAndRotation(
                head.position + right * m_reachOffset.x + Vector3.up * m_reachOffset.y + forward * m_reachOffset.z,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        // Re-places the target in front of the head and stops any motion it had, so a
        // thrown or dropped cube does not keep sliding after it comes back.
        private void ResetReachTarget()
        {
            if (m_reachTarget == null) return;

            PlaceReachTarget();

            var rb = m_reachTarget.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // Fully qualified rather than imported: Oculus.Interaction declares types that
        // collide with names used here.
        private void CollectContentRoots()
        {
            m_contentRoots.Clear();

            if (m_mirrorContentRoots != null)
            {
                foreach (Transform root in m_mirrorContentRoots)
                {
                    if (root != null) m_contentRoots.Add(root);
                }
            }

            if (m_includeHandVisuals)
            {
                foreach (var hand in FindObjectsByType<Oculus.Interaction.HandVisual>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!m_contentRoots.Contains(hand.transform))
                        m_contentRoots.Add(hand.transform);
                }
            }
        }

        // Re-applied every frame, not just at startup, to catch the bone objects the
        // hand skeleton spawns as it initialises. Hand-rolled recursion rather than
        // GetComponentsInChildren so this allocates nothing per frame.
        private void ApplyMirrorContentLayer()
        {
            if (m_contentRoots.Count == 0) return;

            int layer = MaskToLayer(m_mirrorContentLayer);
            if (layer < 0) return;

            foreach (Transform root in m_contentRoots)
            {
                if (root != null)
                    SetLayerRecursive(root, layer);
            }
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            if (t.gameObject.layer != layer)
                t.gameObject.layer = layer;

            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }

        // Cameras want a mask, GameObject.layer wants an index.
        private static int MaskToLayer(int mask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0) return i;
            }
            return -1;
        }

        private void Update()
        {
            // Ahead of the quad check: the quad is created a frame after the content
            // camera, and it must never composite a render texture nothing has drawn
            // into yet.
            ApplyMirrorContentLayer();
            RenderContent();
            LogDiagnostics();

            // A on the right controller puts the reach target back where it started.
            if (m_contentCamera != null && OVRInput.GetDown(OVRInput.Button.One))
                ResetReachTarget();

            if (m_screenQuad == null) return;

            m_mirrorMaterial.SetFloat("_UVScale", m_imageScale);
            m_mirrorMaterial.SetFloat("_MirrorRight", m_mirrorRightSide ? 1f : 0f);
            m_mirrorMaterial.SetFloat("_Separation", m_separation);
            SetSquareUniforms();
        }

        // Rendered every frame rather than only when a new camera frame arrives,
        // because mirror content can move on its own (the sample cube is grabbable).
        private void RenderContent()
        {
            if (m_contentCamera == null || !m_cameraAccess.IsPlaying) return;

            // GetCameraPose returns the pose at the image's timestamp, not "now", so
            // virtual content stays registered to the image it is composited into.
            Pose pose = m_cameraAccess.GetCameraPose();
            m_contentCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            m_contentCamera.projectionMatrix = BuildIntrinsicProjection(
                m_contentCamera.nearClipPlane, m_contentCamera.farClipPlane);

            m_contentCamera.Render();
        }

        // One log entry per interval covering every renderer that should reach the
        // mirror: everything on the mirror layer plus everything under the content
        // roots (which catches anything the layer pass has missed). The cube is
        // included on purpose, as a known-good baseline to compare the hands against.
        private void LogDiagnostics()
        {
            if (!m_logDiagnostics || Time.unscaledTime < m_nextDiagnosticsTime) return;
            m_nextDiagnosticsTime = Time.unscaledTime + m_diagnosticsInterval;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[MirrorV8] ---- diagnostics ----");

            sb.AppendLine(m_mirrorContentShader == null
                ? "[MirrorV8] replacement shader: NOT ASSIGNED"
                : $"[MirrorV8] replacement shader: {m_mirrorContentShader.name} supported={m_mirrorContentShader.isSupported}");

            if (m_contentCamera == null || !m_cameraAccess.IsPlaying)
            {
                sb.AppendLine($"[MirrorV8] content camera: not created yet (cameraPlaying={m_cameraAccess.IsPlaying})");
                Debug.Log(sb.ToString());
                return;
            }

            int layer = MaskToLayer(m_mirrorContentLayer);
            Pose pose = m_cameraAccess.GetCameraPose();
            sb.AppendLine($"[MirrorV8] content camera: mask={m_contentCamera.cullingMask} layer={layer} " +
                          $"rt={m_contentTexture.width}x{m_contentTexture.height} pos={pose.position} fwd={pose.rotation * Vector3.forward}");
            sb.AppendLine($"[MirrorV8] main camera mask={m_mainCamera.cullingMask} (bit {layer} set={(m_mainCamera.cullingMask & (1 << layer)) != 0})");

            if (m_contentRoots.Count > 0)
            {
                foreach (Transform root in m_contentRoots)
                {
                    sb.AppendLine(root == null
                        ? "[MirrorV8] root: NULL"
                        : $"[MirrorV8] root: {root.name} active={root.gameObject.activeInHierarchy} layer={root.gameObject.layer} children={root.childCount}");
                }
            }

            int count = 0;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.gameObject.layer != layer && !IsUnderContentRoot(r.transform)) continue;
                count++;

                Vector3 c = r.bounds.center;
                Vector3 local = Quaternion.Inverse(pose.rotation) * (c - pose.position);
                Vector2 vp = m_cameraAccess.WorldToViewportPoint(c, pose);
                string mesh = r is SkinnedMeshRenderer smr
                    ? $" mesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "NULL")} bones={smr.bones.Length}"
                    : "";

                sb.AppendLine($"[MirrorV8]   {r.GetType().Name} '{r.name}' enabled={r.enabled} " +
                              $"active={r.gameObject.activeInHierarchy} layer={r.gameObject.layer}{mesh} " +
                              $"boundsSize={r.bounds.size} depth={local.z:F2} viewport={vp}");
            }
            sb.AppendLine($"[MirrorV8] {count} renderer(s) reported");

            // Interaction: is anything able to grab, and how far are the hands from
            // the target? Hover/Select states show the SDK seeing the hand at all.
            foreach (var interactor in FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractor>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"[MirrorV8] HandGrabInteractor '{interactor.name}' " +
                              $"active={interactor.gameObject.activeInHierarchy} enabled={interactor.enabled} state={interactor.State}");
            }
            foreach (var grab in FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractable>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"[MirrorV8] HandGrabInteractable '{grab.name}' " +
                              $"active={grab.gameObject.activeInHierarchy} enabled={grab.enabled} state={grab.State}");
            }
            if (m_reachTarget != null)
            {
                sb.AppendLine($"[MirrorV8] reach target at {m_reachTarget.position}");
                foreach (var hand in FindObjectsByType<Oculus.Interaction.HandVisual>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    foreach (var smr in hand.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (!smr.enabled) continue;
                        sb.AppendLine($"[MirrorV8] hand '{hand.name}' centre {smr.bounds.center} " +
                                      $"-> target {Vector3.Distance(smr.bounds.center, m_reachTarget.position):F2} m");
                    }
                }
            }

            Debug.Log(sb.ToString());
        }

        private bool IsUnderContentRoot(Transform t)
        {
            if (m_contentRoots.Count == 0) return false;
            foreach (Transform root in m_contentRoots)
            {
                if (root != null && t.IsChildOf(root)) return true;
            }
            return false;
        }

        // Reproduces PassthroughCameraAccess.WorldToViewportPoint as a clip-space
        // matrix. That method maps a camera-space point (x, y, z) to viewport
        // ((x / z * fx + px - cropX) / cropW, (y / z * fy + py - cropY) / cropH).
        // Unity's view space looks down -Z, so z_view = -z, which is where the signs
        // in the bottom rows come from.
        private Matrix4x4 BuildIntrinsicProjection(float near, float far)
        {
            PassthroughCameraAccess.CameraIntrinsics intrinsics = m_cameraAccess.Intrinsics;
            Rect crop = CalcSensorCropRegion(intrinsics.SensorResolution, m_cameraAccess.CurrentResolution);

            var m = new Matrix4x4();
            m[0, 0] = 2f * intrinsics.FocalLength.x / crop.width;
            m[0, 2] = 1f - 2f * (intrinsics.PrincipalPoint.x - crop.x) / crop.width;
            m[1, 1] = 2f * intrinsics.FocalLength.y / crop.height;
            m[1, 2] = 1f - 2f * (intrinsics.PrincipalPoint.y - crop.y) / crop.height;
            m[2, 2] = -(far + near) / (far - near);
            m[2, 3] = -2f * far * near / (far - near);
            m[3, 2] = -1f;
            return m;
        }

        // The requested resolution is a centre crop of the sensor, so the crop has to
        // be folded into the projection too. Mirrors the SDK's private equivalent.
        private static Rect CalcSensorCropRegion(Vector2Int sensorResolution, Vector2Int currentResolution)
        {
            Vector2 sensor = sensorResolution;
            Vector2 scaleFactor = (Vector2)currentResolution / sensor;
            scaleFactor /= Mathf.Max(scaleFactor.x, scaleFactor.y);
            return new Rect(
                sensor.x * (1f - scaleFactor.x) * 0.5f,
                sensor.y * (1f - scaleFactor.y) * 0.5f,
                sensor.x * scaleFactor.x,
                sensor.y * scaleFactor.y);
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

            if (m_contentCamera != null)
                Destroy(m_contentCamera.gameObject);

            if (m_contentTexture != null)
            {
                m_contentTexture.Release();
                Destroy(m_contentTexture);
            }

            if (m_mirrorMaterial != null)
                Destroy(m_mirrorMaterial);
        }
    }
}
